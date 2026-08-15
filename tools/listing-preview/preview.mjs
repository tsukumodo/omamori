#!/usr/bin/env node
// つくも堂 VPMリスティング ローカルプレビュー
//
// なぜ必要か:
//   Website/index.html と Website/app.js は Scriban テンプレートで、
//   GitHub Actions 上の vrchat-community/package-list-action が描画するまで
//   実際の見た目を確認できない。本番デプロイして初めて分かる、では
//   デザイン改修のたびに待ち時間が発生してしまうため、ダミーデータで
//   ローカル描画するための簡易ツール。
//
// 方針:
//   - Node.js 22 標準ライブラリのみで動作する（npm依存・package.jsonは追加しない）
//   - Website/ 配下を _preview/ にそのままコピーしつつ、テンプレートファイル
//     (index.html, app.js) だけを Scriban 風プレースホルダの簡易処理で描画する
//   - 未知のプレースホルダ（フィクスチャに無いフィールド等）は握りつぶさず、
//     stderr に警告を出し、出力にも `{{ ... }}` の形のまま残す
//     → テンプレート契約（DESIGN_SPEC.md §2）を壊した変更に気づけるようにするため
//
// 使い方:
//   node tools/listing-preview/preview.mjs            # _preview/ に生成するだけ
//   node tools/listing-preview/preview.mjs --serve     # 生成 + http://localhost:4173 で配信
//   node tools/listing-preview/preview.mjs --serve --port 5000

import {
  readdirSync,
  statSync,
  readFileSync,
  writeFileSync,
  mkdirSync,
  copyFileSync,
  rmSync,
  existsSync,
  createReadStream,
} from 'node:fs';
import { join, dirname, relative, extname, sep } from 'node:path';
import { fileURLToPath } from 'node:url';
import http from 'node:http';

const __dirname = dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = join(__dirname, '..', '..');
const WEBSITE_DIR = join(REPO_ROOT, 'Website');
const OUT_DIR = join(REPO_ROOT, '_preview');
const FIXTURE_PATH = join(__dirname, 'fixtures', 'listing.json');

// Scribanテンプレートとして「描画」するファイル。Website直下のこの2つだけが
// テンプレート契約の対象（DESIGN_SPEC.md §2）。それ以外（画像・favicon・vendorの
// JSライブラリ等）はそのままコピーする。
const TEMPLATE_FILES = new Set(['index.html', 'app.js']);

let warnCount = 0;
function warn(message) {
  warnCount++;
  process.stderr.write(`[preview] WARNING: ${message}\n`);
}

// ---------------------------------------------------------------------------
// Scriban風ミニテンプレートエンジン
// ---------------------------------------------------------------------------

// {{ ... }} / {{~ ... ~}} をすべて抜き出し、テキストとタグの列に分解する
function tokenize(src) {
  const tokens = [];
  const tagRe = /\{\{(~?)\s*([\s\S]*?)\s*(~?)\}\}/g;
  let lastIndex = 0;
  let match;
  while ((match = tagRe.exec(src)) !== null) {
    if (match.index > lastIndex) {
      tokens.push({ type: 'text', value: src.slice(lastIndex, match.index) });
    }
    tokens.push({
      type: 'tag',
      leftTilde: match[1] === '~',
      content: match[2],
      rightTilde: match[3] === '~',
    });
    lastIndex = tagRe.lastIndex;
  }
  if (lastIndex < src.length) {
    tokens.push({ type: 'text', value: src.slice(lastIndex) });
  }

  // Scribanの `~` は隣接する空白(改行含む、1行分)をトリムする。
  // 完全再現はしないが、ループの中身が縦に間延びしない程度の簡易再現。
  for (let i = 0; i < tokens.length; i++) {
    const tag = tokens[i];
    if (tag.type !== 'tag') continue;
    if (tag.leftTilde && tokens[i - 1]?.type === 'text') {
      tokens[i - 1].value = tokens[i - 1].value.replace(/[ \t]*\n?[ \t]*$/, '');
    }
    if (tag.rightTilde && tokens[i + 1]?.type === 'text') {
      tokens[i + 1].value = tokens[i + 1].value.replace(/^[ \t]*\n?[ \t]*/, '');
    }
  }
  return tokens;
}

const FOR_RE = /^for\s+(\w+)\s+in\s+(.+)$/s;

// トークン列 → ノード木。{{~ for x in y ~}} ... {{~ end ~}} のネストに対応するため、
// for に出会ったら再帰し、その再帰呼び出しが自分自身の対応する end を消費する。
function parseNodes(tokens, pos) {
  const nodes = [];
  while (pos < tokens.length) {
    const token = tokens[pos];
    if (token.type === 'text') {
      nodes.push({ type: 'text', value: token.value });
      pos++;
      continue;
    }

    const content = token.content.trim();

    if (content === 'end') {
      // このブロックの終端。end自体を消費して呼び出し元(forノード)に返す
      return { nodes, pos: pos + 1 };
    }

    const forMatch = content.match(FOR_RE);
    if (forMatch) {
      const [, varName, listExprRaw] = forMatch;
      const inner = parseNodes(tokens, pos + 1);
      nodes.push({
        type: 'for',
        varName,
        listExpr: listExprRaw.trim(),
        children: inner.nodes,
      });
      pos = inner.pos;
      continue;
    }

    nodes.push({ type: 'expr', content });
    pos++;
  }
  return { nodes, pos };
}

// scope上でドット記法のパスを解決する。scopeはループ変数を持つ入れ子オブジェクトで、
// Object.create(parentScope) によって親スコープの変数も見えるようにしている。
function resolvePath(scope, path) {
  const segments = path.trim().split('.');
  const first = segments[0];
  if (!(first in scope)) return undefined;
  let value = scope[first];
  for (let i = 1; i < segments.length; i++) {
    if (value === null || value === undefined) return undefined;
    value = value[segments[i]];
  }
  return value;
}

function stringify(value) {
  if (value === undefined || value === null) return '';
  return String(value);
}

// `if COND; OUT; end;` 形式(DESIGN_SPEC.md §2 記載の条件式)。
// 「値があればそれを出力、無ければ空」の意味として扱う。
const IF_RE = /^if\s+(.+?);\s*(.+?);\s*end;?$/s;

function evalExpr(content, scope, filename) {
  const ifMatch = content.match(IF_RE);
  if (ifMatch) {
    const [, condExprRaw, outExprRaw] = ifMatch;
    const condExpr = condExprRaw.trim();
    const outExpr = outExprRaw.trim();
    const condValue = resolvePath(scope, condExpr);
    if (condValue === undefined) {
      warn(`未知の変数 "${condExpr}"（if条件, ${filename} 内 "{{ ${content} }}"）`);
    }
    if (condValue) {
      const outValue = resolvePath(scope, outExpr);
      return stringify(outValue);
    }
    return '';
  }

  const value = resolvePath(scope, content);
  if (value === undefined) {
    warn(`未知のプレースホルダ "{{ ${content} }}" (${filename})`);
    return `{{ ${content} }}`; // 握りつぶさず出力にも残す
  }
  if (typeof value === 'object') {
    warn(`"{{ ${content} }}" はオブジェクト/配列を単純展開しようとしています (${filename})。for でループしてください`);
    return `{{ ${content} }}`;
  }
  return stringify(value);
}

function renderNodes(nodes, scope, filename) {
  let out = '';
  for (const node of nodes) {
    if (node.type === 'text') {
      out += node.value;
    } else if (node.type === 'expr') {
      out += evalExpr(node.content, scope, filename);
    } else if (node.type === 'for') {
      const list = resolvePath(scope, node.listExpr);
      if (list === undefined) {
        warn(`未知のリスト "${node.listExpr}"（for ${node.varName} in ${node.listExpr}, ${filename}）`);
        continue;
      }
      if (!Array.isArray(list)) {
        warn(`"${node.listExpr}" は配列ではありません (${filename})`);
        continue;
      }
      for (const item of list) {
        const childScope = Object.create(scope);
        childScope[node.varName] = item;
        out += renderNodes(node.children, childScope, filename);
      }
    }
  }
  return out;
}

function renderTemplate(src, data, filename) {
  const tokens = tokenize(src);
  const { nodes, pos } = parseNodes(tokens, 0);
  if (pos < tokens.length) {
    warn(`テンプレート解析中に対応しない "end" が見つかった可能性があります (${filename})`);
  }
  const rootScope = Object.create(null);
  Object.assign(rootScope, data);
  return renderNodes(nodes, rootScope, filename);
}

// ---------------------------------------------------------------------------
// ビルド本体
// ---------------------------------------------------------------------------

function walkFiles(dir, baseDir, onFile) {
  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    const fullPath = join(dir, entry.name);
    if (entry.isDirectory()) {
      walkFiles(fullPath, baseDir, onFile);
    } else if (entry.isFile()) {
      onFile(fullPath, relative(baseDir, fullPath));
    }
  }
}

function buildPreview() {
  if (!existsSync(WEBSITE_DIR)) {
    console.error(`[preview] Website ディレクトリが見つかりません: ${WEBSITE_DIR}`);
    process.exit(1);
  }
  if (!existsSync(FIXTURE_PATH)) {
    console.error(`[preview] フィクスチャが見つかりません: ${FIXTURE_PATH}`);
    process.exit(1);
  }

  const data = JSON.parse(readFileSync(FIXTURE_PATH, 'utf8'));

  rmSync(OUT_DIR, { recursive: true, force: true });
  mkdirSync(OUT_DIR, { recursive: true });

  let fileCount = 0;
  let renderedCount = 0;
  walkFiles(WEBSITE_DIR, WEBSITE_DIR, (fullPath, relPath) => {
    const outPath = join(OUT_DIR, relPath);
    mkdirSync(dirname(outPath), { recursive: true });

    const isTemplate = TEMPLATE_FILES.has(relPath.split(sep).join('/'));
    if (isTemplate) {
      const src = readFileSync(fullPath, 'utf8');
      const rendered = renderTemplate(src, data, relPath);
      writeFileSync(outPath, rendered, 'utf8');
      renderedCount++;
      console.log(`[preview] rendered : ${relPath}`);
    } else {
      copyFileSync(fullPath, outPath);
      console.log(`[preview] copied   : ${relPath}`);
    }
    fileCount++;
  });

  console.log(`[preview] 完了: ${fileCount} 件のファイルを _preview/ に出力（うちテンプレート描画 ${renderedCount} 件）`);
  if (warnCount > 0) {
    console.error(`[preview] 警告 ${warnCount} 件。上記の WARNING を確認してください（テンプレート契約が壊れている可能性があります）`);
  }
}

// ---------------------------------------------------------------------------
// 簡易静的サーバ（node:http のみ）
// ---------------------------------------------------------------------------

const MIME_TYPES = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.mjs': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.jpeg': 'image/jpeg',
  '.svg': 'image/svg+xml',
  '.ico': 'image/x-icon',
};

function serve(port) {
  const server = http.createServer((req, res) => {
    try {
      const urlPath = decodeURIComponent((req.url || '/').split('?')[0]);
      const relPath = urlPath === '/' ? 'index.html' : urlPath.replace(/^\/+/, '');
      const filePath = join(OUT_DIR, relPath);

      // OUT_DIR の外を指すパスは拒否(簡易的なディレクトリトラバーサル対策)
      if (!filePath.startsWith(OUT_DIR)) {
        res.writeHead(403, { 'Content-Type': 'text/plain; charset=utf-8' });
        res.end('403 Forbidden');
        return;
      }

      if (!existsSync(filePath) || statSync(filePath).isDirectory()) {
        res.writeHead(404, { 'Content-Type': 'text/plain; charset=utf-8' });
        res.end('404 Not Found');
        return;
      }

      const ext = extname(filePath).toLowerCase();
      res.writeHead(200, { 'Content-Type': MIME_TYPES[ext] || 'application/octet-stream' });
      createReadStream(filePath).pipe(res);
    } catch (err) {
      res.writeHead(500, { 'Content-Type': 'text/plain; charset=utf-8' });
      res.end(`500 Internal Server Error: ${err.message}`);
    }
  });

  server.listen(port, () => {
    console.log(`[preview] http://localhost:${port}/ で配信中（Ctrl+Cで終了）`);
  });

  return server;
}

// ---------------------------------------------------------------------------
// CLI
// ---------------------------------------------------------------------------

function parseArgs(argv) {
  const args = { serve: false, port: 4173 };
  for (let i = 0; i < argv.length; i++) {
    const arg = argv[i];
    if (arg === '--serve') {
      args.serve = true;
    } else if (arg === '--port') {
      args.port = Number(argv[++i]);
    } else if (arg.startsWith('--port=')) {
      args.port = Number(arg.split('=')[1]);
    } else if (arg === '--help' || arg === '-h') {
      args.help = true;
    }
  }
  return args;
}

function printHelp() {
  console.log(`使い方: node tools/listing-preview/preview.mjs [--serve] [--port <番号>]

  --serve         生成後、_preview/ を静的配信する簡易HTTPサーバを起動する
  --port <番号>   --serve と併用。配信ポート番号(デフォルト: 4173)
`);
}

const args = parseArgs(process.argv.slice(2));
if (args.help) {
  printHelp();
  process.exit(0);
}

buildPreview();

if (args.serve) {
  if (!Number.isInteger(args.port) || args.port <= 0) {
    console.error(`[preview] 不正なポート番号です: ${args.port}`);
    process.exit(1);
  }
  serve(args.port);
}

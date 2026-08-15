// つくも堂 VPMリスティング — ページ挙動
//
// このファイルは Scriban テンプレートとして描画される（DESIGN_SPEC.md §2）。
// 冒頭の LISTING_URL 定数と PACKAGES オブジェクト生成ブロックのプレースホルダは
// ビルド時に package-list-action が実データへ置き換える。書式を変えたり
// 削除したりしないこと。
//
// Fluent UI 撤去後にこのファイルが持つ責務は次の6つだけ（INTEGRATION_SPEC.md §4。
// 「二つの道」のタブUIは第8ラウンドAで廃止し、両方の道を常時表示にしたため
// タブ制御の責務は無くなった）:
//   1. LISTING_URL / PACKAGES の受け取り（Scriban）
//   2. VCC追加ボタン（ヒーロー + 二つの道のVCC欄 + パッケージ詳細ダイアログ。
//      VCCへの追加はリスティング単位の操作でパッケージ単位には出来ないため、
//      棚の各行には個別の追加ボタンを置かない。D-6）
//   3. URLコピー
//   4. パッケージ詳細 <dialog> の開閉と内容差し込み
//   5. 検索（2件以上のときだけ有効化）
//   6. マスコットの目線追従・瞬き

const LISTING_URL = "{{ listingInfo.Url }}";

const PACKAGES = {
{{~ for package in packages ~}}
  "{{ package.Name }}": {
    name: "{{ package.Name }}",
    displayName: "{{ if package.DisplayName; package.DisplayName; end; }}",
    description: "{{ if package.Description; package.Description; end; }}",
    version: "{{ package.Version }}",
    author: {
      name: "{{ if package.Author.Name; package.Author.Name; end; }}",
      url: "{{ if package.Author.Url; package.Author.Url; end; }}",
    },
    dependencies: {
      {{~ for dependency in package.Dependencies ~}}
        "{{ dependency.Name }}": "{{ dependency.Version }}",
      {{~ end ~}}
    },
    keywords: [
      {{~ for keyword in package.Keywords ~}}
        "{{ keyword }}",
      {{~ end ~}}
    ],
    license: "{{ package.License }}",
    licensesUrl: "{{ package.LicensesUrl }}",
  },
{{~ end ~}}
};

// ─── 1. VCC追加ボタン ────────────────────────────────────────────────
function initVccButtons() {
  const goToVcc = () => {
    window.location.assign(`vcc://vpm/addRepo?url=${encodeURIComponent(LISTING_URL)}`);
  };
  // 棚の各行には .tana-add を置かない（D-6）。VCCへの追加はリスティング
  // （リポジトリ）単位の操作で、VPMにはパッケージ単体を狙い撃ちで追加する
  // 手段が無いため、行ごとに data-package-id を持たせても機能しない
  // 死んだ属性になっていた。
  document.querySelectorAll('#addToVcc, .vcc-trigger').forEach((button) => {
    button.addEventListener('click', goToVcc);
  });
}

// ─── 2. URLコピー ────────────────────────────────────────────────────
// D-4: コピー成功はこれまでボタン自身のラベル変更（「写しました」）だけで
// 伝えていたが、見た目の変化なのでスクリーンリーダー利用者には届かない。
// 共有の role="status" 領域（#a11yStatus）にも同じ内容を流す。
// 同文言が連続しても再アナウンスされるよう、一度空にしてから次のフレームで
// 差し込む（同一テキストへの上書きだけだと変化なしと判断され黙殺する
// 実装があるため）。
function announce(message) {
  const status = document.getElementById('a11yStatus');
  if (!status) return;
  status.textContent = '';
  window.requestAnimationFrame(() => {
    status.textContent = message;
  });
}

function copyListingUrl(input, button) {
  const text = input.value;
  const showCopied = () => {
    const original = button.dataset.originalLabel ?? button.textContent;
    button.dataset.originalLabel = original;
    button.textContent = '写しました';
    button.classList.add('is-copied');
    window.clearTimeout(button._copyResetTimer);
    button._copyResetTimer = window.setTimeout(() => {
      button.textContent = original;
      button.classList.remove('is-copied');
    }, 1800);
    announce('リスティングURLをコピーしました');
    blinkMascotOnce();
  };

  if (navigator.clipboard && navigator.clipboard.writeText) {
    navigator.clipboard.writeText(text).then(showCopied, () => {
      // クリップボードへの書き込みに失敗した環境向けフォールバック
      input.select();
    });
  } else {
    // navigator.clipboard が無い環境向けフォールバック
    input.select();
  }
}

function initCopyButtons() {
  // ヒーローの生URL欄は廃止した（D-14）。「二つの道」のURLタブにのみ残す。
  const wirings = [
    ['listingUrlPanel', 'copyListingUrlPanel'],
  ];
  wirings.forEach(([inputId, buttonId]) => {
    const input = document.getElementById(inputId);
    const button = document.getElementById(buttonId);
    if (!input || !button) return;
    button.addEventListener('click', () => copyListingUrl(input, button));
  });
}

// ─── 3. パッケージ詳細ダイアログ ─────────────────────────────────────
function initPackageDialog() {
  const dialog = document.getElementById('packageInfoDialog');
  if (!dialog) return;

  const els = {
    name: document.getElementById('packageInfoName'),
    id: document.getElementById('packageInfoId'),
    version: document.getElementById('packageInfoVersion'),
    description: document.getElementById('packageInfoDescription'),
    author: document.getElementById('packageInfoAuthor'),
    dependencies: document.getElementById('packageInfoDependencies'),
    keywordsWrap: document.getElementById('packageInfoKeywordsWrap'),
    keywords: document.getElementById('packageInfoKeywords'),
    licenseWrap: document.getElementById('packageInfoLicenseWrap'),
    license: document.getElementById('packageInfoLicense'),
  };
  const closeButton = document.getElementById('packageInfoDialogClose');

  function openFor(packageId) {
    const info = PACKAGES[packageId];
    if (!info) {
      console.error(`[つくも堂] パッケージ情報が見つかりません: ${packageId}`);
      return;
    }

    els.name.textContent = info.displayName;
    els.id.textContent = packageId;
    els.version.textContent = `v${info.version}`;
    els.description.textContent = info.description;
    els.author.textContent = info.author.name;
    els.author.href = info.author.url || '#';

    els.dependencies.innerHTML = '';
    Object.entries(info.dependencies).forEach(([depName, depVersion]) => {
      const li = document.createElement('li');
      li.textContent = `${depName} @ v${depVersion}`;
      els.dependencies.appendChild(li);
    });

    if (info.keywords.length === 0) {
      els.keywordsWrap.hidden = true;
    } else {
      els.keywordsWrap.hidden = false;
      els.keywords.innerHTML = '';
      info.keywords.forEach((keyword) => {
        const chip = document.createElement('span');
        chip.className = 'chip';
        chip.textContent = keyword;
        els.keywords.appendChild(chip);
      });
    }

    if (!info.license && !info.licensesUrl) {
      els.licenseWrap.hidden = true;
    } else {
      els.licenseWrap.hidden = false;
      els.license.textContent = info.license || 'ライセンスを見る';
      els.license.href = info.licensesUrl || '#';
    }

    // ダイアログのCTA「おまもりを授かる」は initVccButtons が
    // .vcc-trigger として共通で配線する（.zip直リンクは第8ラウンドCで削除）。
    dialog.showModal();
  }

  document.querySelectorAll('.tana-info').forEach((button) => {
    button.addEventListener('click', () => openFor(button.dataset.packageId));
  });

  closeButton.addEventListener('click', () => dialog.close());

  // 背景（::backdrop）クリックで閉じる。ダイアログ自身がクリックされた
  // (=内側のコンテンツではない) 場合のみ閉じる、というネイティブdialogの定石。
  dialog.addEventListener('click', (event) => {
    if (event.target === dialog) dialog.close();
  });
}

// ─── 4. 検索（2件以上のときだけ） ────────────────────────────────────
function initSearch() {
  const list = document.getElementById('tanaList');
  const searchBlock = document.getElementById('tanaSearch');
  const emptyMessage = document.getElementById('tanaEmpty');
  if (!list || !searchBlock) return;

  const items = Array.from(list.querySelectorAll('.tana-item'));
  if (items.length < 2) return; // 1件以下なら検索欄は出さない

  searchBlock.hidden = false;
  const input = document.getElementById('packageSearch');
  input.addEventListener('input', () => {
    const query = input.value.trim().toLowerCase();
    let visibleCount = 0;
    items.forEach((item) => {
      const name = (item.dataset.packageName || '').toLowerCase();
      const id = (item.dataset.packageId || '').toLowerCase();
      const matches = query === '' || name.includes(query) || id.includes(query);
      item.hidden = !matches;
      if (matches) visibleCount += 1;
    });
    // D-2: 0件のとき棚が唐突に空白にならないよう案内を出す。
    // #tanaEmpty は role="status" aria-live="polite" なので、非表示から
    // 表示に切り替わったこと自体がスクリーンリーダーにも伝わる。
    if (emptyMessage) emptyMessage.hidden = visibleCount > 0;
  });
}

// ─── 5. マスコットの目線追従・瞬き ───────────────────────────────────
let blinkMascotOnce = () => {};

function initMascot() {
  const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)');
  if (reduceMotion.matches) return; // この機能ごと起動しない

  const pupils = Array.from(document.querySelectorAll('.mascot-pupil'));
  const lids = Array.from(document.querySelectorAll('.mascot-lid'));
  if (pupils.length === 0) return;

  let targetX = 0;
  let targetY = 0;
  let rafId = null;

  function applyGaze() {
    rafId = null;
    pupils.forEach((pupil) => {
      pupil.setAttribute('transform', `translate(${targetX.toFixed(2)}, ${targetY.toFixed(2)})`);
    });
  }

  window.addEventListener('pointermove', (event) => {
    const nx = (event.clientX / window.innerWidth) * 2 - 1; // -1〜1
    const ny = (event.clientY / window.innerHeight) * 2 - 1;
    targetX = Math.max(-2, Math.min(2, nx * 2));
    targetY = Math.max(-1.5, Math.min(1.5, ny * 1.5));
    if (rafId === null) rafId = requestAnimationFrame(applyGaze);
  }, { passive: true });

  function setLids(opacity) {
    lids.forEach((lid) => { lid.style.opacity = String(opacity); });
  }

  let blinkTimer = null;
  function scheduleBlink() {
    const delay = 2600 + Math.random() * 4200;
    blinkTimer = window.setTimeout(() => {
      blinkOnceInternal();
      scheduleBlink();
    }, delay);
  }

  function blinkOnceInternal() {
    setLids(1);
    window.setTimeout(() => setLids(0), 130);
  }

  blinkMascotOnce = blinkOnceInternal;
  scheduleBlink();

  // 途中でreduced-motionに切り替わった場合は追従・瞬きを止める
  reduceMotion.addEventListener('change', (event) => {
    if (event.matches) {
      window.clearTimeout(blinkTimer);
      setLids(0);
      pupils.forEach((pupil) => pupil.setAttribute('transform', 'translate(0,0)'));
      blinkMascotOnce = () => {};
    }
  });
}

// ─── 起動 ────────────────────────────────────────────────────────────
initVccButtons();
initCopyButtons();
initPackageDialog();
initSearch();
initMascot();

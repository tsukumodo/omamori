using System.Runtime.CompilerServices;

// internal メンバー（SanitizeKey・FixHistoryStore 等）を EditMode テストから検証できるようにする
[assembly: InternalsVisibleTo("com.tsukumodo.avatar-omamori.tests.editor")]

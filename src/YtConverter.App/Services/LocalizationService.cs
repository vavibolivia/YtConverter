using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text.Json;

namespace YtConverter.App.Services;

public sealed class LocalizationService : INotifyPropertyChanged
{
    public static LocalizationService Instance { get; } = new();

    public sealed record LanguageOption(string Code, string Label);
    public static readonly IReadOnlyList<LanguageOption> Languages = new LanguageOption[]
    {
        new("en", "English"),
        new("ko", "한국어"),
        new("ja", "日本語"),
        new("zh", "简体中文"),
        new("es", "Español"),
        new("fr", "Français"),
        new("de", "Deutsch"),
        new("pt", "Português"),
    };

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "YtConverter", "settings.json");

    private string _lang = "en";
    public string Language
    {
        get => _lang;
        set
        {
            if (!Translations.ContainsKey(value)) value = "en";
            if (_lang == value) return;
            _lang = value;
            Save();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
        }
    }

    public string this[string key]
    {
        get
        {
            if (Translations.TryGetValue(_lang, out var d) && d.TryGetValue(key, out var v)) return v;
            if (Translations["en"].TryGetValue(key, out var fallback)) return fallback;
            return key;
        }
    }

    public string Format(string key, params object[] args)
    {
        try { return string.Format(this[key], args); }
        catch { return this[key]; }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private LocalizationService() => Load();

    private sealed class SettingsDto { public string Lang { get; set; } = "en"; }

    private void Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var s = JsonSerializer.Deserialize<SettingsDto>(json);
                if (s is not null && Translations.ContainsKey(s.Lang)) _lang = s.Lang;
            }
        }
        catch { }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(new SettingsDto { Lang = _lang }));
        }
        catch { }
    }

    private static readonly Dictionary<string, Dictionary<string, string>> Translations = new()
    {
        ["en"] = new()
        {
            ["title"] = "YouTube Converter",
            ["subtitle"] = "Paste a link → pick a format → Start. Done.",
            ["open_folder"] = "📂 Open folder",
            ["url_placeholder"] = "Paste YouTube link(s) here — drag & drop works — Ctrl+Enter to add",
            ["mp3"] = "MP3 (audio)",
            ["mp4"] = "MP4 (video)",
            ["concurrent"] = "Concurrent",
            ["add"] = "➕ Add to list",
            ["start_all"] = "▶ Start all",
            ["cancel_all"] = "⏹ Cancel all",
            ["clear_completed"] = "🧹 Clear completed",
            ["jobs_header"] = "📋 Conversion jobs",
            ["items"] = "items",
            ["empty_title"] = "No jobs yet",
            ["empty_body"] = "Paste a YouTube link above and press ‘Add to list’.\nYou can also drop a link anywhere in this window.",
            ["save_folder"] = "💾 Save folder",
            ["change"] = "Change",
            ["clipboard_title"] = "📋 YouTube link detected in your clipboard",
            ["accept"] = "Add",
            ["dismiss"] = "Dismiss",
            ["drop_here"] = "Drop here — we'll add it to the queue",
            ["language"] = "Language",
            ["status_idle"] = "Waiting",
            ["status_resolving"] = "Parsing stream",
            ["status_downloading"] = "Downloading {0:P0}",
            ["status_muxing"] = "Converting",
            ["status_completed"] = "Done",
            ["status_canceled"] = "Canceled",
            ["status_failed"] = "Error",
            ["err_live"] = "Live streams are not supported.",
            ["err_unavailable"] = "Video is deleted or private.",
            ["err_unplayable"] = "Video unplayable (age or region restricted).",
            ["err_network"] = "Network error. Please retry.",
            ["err_io"] = "Not enough disk space or cannot write the file.",
        },
        ["ko"] = new()
        {
            ["title"] = "YouTube 변환기",
            ["subtitle"] = "링크 붙여넣기 → 포맷 선택 → 시작. 끝.",
            ["open_folder"] = "📂 저장 폴더 열기",
            ["url_placeholder"] = "여기에 유튜브 링크를 붙여넣으세요 (여러 개 OK · 드래그해도 돼요 · Ctrl+Enter 로 추가)",
            ["mp3"] = "MP3 (오디오)",
            ["mp4"] = "MP4 (영상)",
            ["concurrent"] = "동시",
            ["add"] = "➕ 목록에 추가",
            ["start_all"] = "▶ 모두 시작",
            ["cancel_all"] = "⏹ 모두 취소",
            ["clear_completed"] = "🧹 완료 정리",
            ["jobs_header"] = "📋 변환 작업",
            ["items"] = "건",
            ["empty_title"] = "아직 변환할 작업이 없어요",
            ["empty_body"] = "위쪽 박스에 유튜브 링크를 붙여넣고 ‘목록에 추가’ 를 눌러주세요.\n창 어디에나 링크를 드래그해도 추가됩니다.",
            ["save_folder"] = "💾 저장 폴더",
            ["change"] = "변경",
            ["clipboard_title"] = "📋 클립보드에서 YouTube 링크 감지",
            ["accept"] = "추가",
            ["dismiss"] = "무시",
            ["drop_here"] = "여기에 놓으세요 — 자동으로 큐에 추가돼요",
            ["language"] = "언어",
            ["status_idle"] = "대기 중",
            ["status_resolving"] = "스트림 해석 중",
            ["status_downloading"] = "다운로드 {0:P0}",
            ["status_muxing"] = "변환 중",
            ["status_completed"] = "완료",
            ["status_canceled"] = "취소됨",
            ["status_failed"] = "오류",
            ["err_live"] = "라이브 스트림은 변환할 수 없습니다.",
            ["err_unavailable"] = "삭제되었거나 비공개 영상입니다.",
            ["err_unplayable"] = "재생 불가 영상입니다 (연령/지역 제한).",
            ["err_network"] = "네트워크 오류. 다시 시도하세요.",
            ["err_io"] = "저장 공간이 부족하거나 파일을 쓸 수 없습니다.",
        },
        ["ja"] = new()
        {
            ["title"] = "YouTube コンバーター",
            ["subtitle"] = "リンクを貼る → 形式を選ぶ → 開始。以上。",
            ["open_folder"] = "📂 保存フォルダーを開く",
            ["url_placeholder"] = "YouTube のリンクを貼り付け (複数可 · ドラッグ可 · Ctrl+Enter で追加)",
            ["mp3"] = "MP3 (音声)",
            ["mp4"] = "MP4 (動画)",
            ["concurrent"] = "同時",
            ["add"] = "➕ 一覧に追加",
            ["start_all"] = "▶ すべて開始",
            ["cancel_all"] = "⏹ すべてキャンセル",
            ["clear_completed"] = "🧹 完了を消去",
            ["jobs_header"] = "📋 変換ジョブ",
            ["items"] = "件",
            ["empty_title"] = "まだジョブがありません",
            ["empty_body"] = "上のボックスに YouTube のリンクを貼り付けて『一覧に追加』を押してください。\nウィンドウのどこにでもリンクをドロップできます。",
            ["save_folder"] = "💾 保存フォルダー",
            ["change"] = "変更",
            ["clipboard_title"] = "📋 クリップボードに YouTube リンクを検出",
            ["accept"] = "追加",
            ["dismiss"] = "無視",
            ["drop_here"] = "ここにドロップ — キューに追加します",
            ["language"] = "言語",
            ["status_idle"] = "待機中",
            ["status_resolving"] = "ストリーム解析中",
            ["status_downloading"] = "ダウンロード {0:P0}",
            ["status_muxing"] = "変換中",
            ["status_completed"] = "完了",
            ["status_canceled"] = "キャンセル",
            ["status_failed"] = "エラー",
            ["err_live"] = "ライブ配信は変換できません。",
            ["err_unavailable"] = "削除済みまたは非公開の動画です。",
            ["err_unplayable"] = "再生できない動画です (年齢/地域制限)。",
            ["err_network"] = "ネットワークエラー。再試行してください。",
            ["err_io"] = "ディスク容量不足またはファイルに書き込めません。",
        },
        ["zh"] = new()
        {
            ["title"] = "YouTube 转换器",
            ["subtitle"] = "粘贴链接 → 选择格式 → 开始。完成。",
            ["open_folder"] = "📂 打开保存文件夹",
            ["url_placeholder"] = "在此粘贴 YouTube 链接 (支持多个 · 可拖放 · Ctrl+Enter 添加)",
            ["mp3"] = "MP3 (音频)",
            ["mp4"] = "MP4 (视频)",
            ["concurrent"] = "并发",
            ["add"] = "➕ 添加到列表",
            ["start_all"] = "▶ 全部开始",
            ["cancel_all"] = "⏹ 全部取消",
            ["clear_completed"] = "🧹 清理已完成",
            ["jobs_header"] = "📋 转换任务",
            ["items"] = "项",
            ["empty_title"] = "暂无任务",
            ["empty_body"] = "在上方粘贴 YouTube 链接并点击『添加到列表』。\n也可以将链接拖到窗口任意位置。",
            ["save_folder"] = "💾 保存文件夹",
            ["change"] = "更改",
            ["clipboard_title"] = "📋 剪贴板中检测到 YouTube 链接",
            ["accept"] = "添加",
            ["dismiss"] = "忽略",
            ["drop_here"] = "拖放到此 — 将自动添加到队列",
            ["language"] = "语言",
            ["status_idle"] = "等待中",
            ["status_resolving"] = "解析流",
            ["status_downloading"] = "下载 {0:P0}",
            ["status_muxing"] = "转换中",
            ["status_completed"] = "完成",
            ["status_canceled"] = "已取消",
            ["status_failed"] = "错误",
            ["err_live"] = "不支持直播流。",
            ["err_unavailable"] = "视频已删除或私密。",
            ["err_unplayable"] = "视频无法播放 (年龄/地区限制)。",
            ["err_network"] = "网络错误。请重试。",
            ["err_io"] = "磁盘空间不足或无法写入文件。",
        },
        ["es"] = new()
        {
            ["title"] = "Conversor de YouTube",
            ["subtitle"] = "Pega el enlace → elige formato → Iniciar. Listo.",
            ["open_folder"] = "📂 Abrir carpeta",
            ["url_placeholder"] = "Pega enlaces de YouTube aquí (varios OK · arrastrar funciona · Ctrl+Enter para añadir)",
            ["mp3"] = "MP3 (audio)",
            ["mp4"] = "MP4 (vídeo)",
            ["concurrent"] = "Simultáneas",
            ["add"] = "➕ Añadir a la lista",
            ["start_all"] = "▶ Iniciar todo",
            ["cancel_all"] = "⏹ Cancelar todo",
            ["clear_completed"] = "🧹 Limpiar completados",
            ["jobs_header"] = "📋 Tareas de conversión",
            ["items"] = "elementos",
            ["empty_title"] = "Aún no hay tareas",
            ["empty_body"] = "Pega un enlace de YouTube arriba y pulsa ‘Añadir a la lista’.\nTambién puedes arrastrar un enlace a la ventana.",
            ["save_folder"] = "💾 Carpeta",
            ["change"] = "Cambiar",
            ["clipboard_title"] = "📋 Enlace de YouTube detectado en el portapapeles",
            ["accept"] = "Añadir",
            ["dismiss"] = "Ignorar",
            ["drop_here"] = "Suelta aquí — se añadirá a la cola",
            ["language"] = "Idioma",
            ["status_idle"] = "En espera",
            ["status_resolving"] = "Analizando flujo",
            ["status_downloading"] = "Descargando {0:P0}",
            ["status_muxing"] = "Convirtiendo",
            ["status_completed"] = "Listo",
            ["status_canceled"] = "Cancelado",
            ["status_failed"] = "Error",
            ["err_live"] = "Las transmisiones en vivo no son compatibles.",
            ["err_unavailable"] = "Vídeo eliminado o privado.",
            ["err_unplayable"] = "Vídeo no reproducible (edad/región).",
            ["err_network"] = "Error de red. Reintenta.",
            ["err_io"] = "Espacio en disco insuficiente o no se puede escribir.",
        },
        ["fr"] = new()
        {
            ["title"] = "Convertisseur YouTube",
            ["subtitle"] = "Colle le lien → choisis le format → Démarrer. Fini.",
            ["open_folder"] = "📂 Ouvrir le dossier",
            ["url_placeholder"] = "Colle des liens YouTube (plusieurs OK · glisser-déposer · Ctrl+Entrée pour ajouter)",
            ["mp3"] = "MP3 (audio)",
            ["mp4"] = "MP4 (vidéo)",
            ["concurrent"] = "Simultanés",
            ["add"] = "➕ Ajouter à la liste",
            ["start_all"] = "▶ Tout démarrer",
            ["cancel_all"] = "⏹ Tout annuler",
            ["clear_completed"] = "🧹 Effacer terminés",
            ["jobs_header"] = "📋 Tâches",
            ["items"] = "éléments",
            ["empty_title"] = "Aucune tâche pour le moment",
            ["empty_body"] = "Colle un lien YouTube ci-dessus et appuie sur ‘Ajouter à la liste’.\nTu peux aussi glisser un lien dans la fenêtre.",
            ["save_folder"] = "💾 Dossier",
            ["change"] = "Changer",
            ["clipboard_title"] = "📋 Lien YouTube détecté dans le presse-papiers",
            ["accept"] = "Ajouter",
            ["dismiss"] = "Ignorer",
            ["drop_here"] = "Déposer ici — ajouté à la file",
            ["language"] = "Langue",
            ["status_idle"] = "En attente",
            ["status_resolving"] = "Analyse du flux",
            ["status_downloading"] = "Téléchargement {0:P0}",
            ["status_muxing"] = "Conversion",
            ["status_completed"] = "Terminé",
            ["status_canceled"] = "Annulé",
            ["status_failed"] = "Erreur",
            ["err_live"] = "Les flux en direct ne sont pas pris en charge.",
            ["err_unavailable"] = "Vidéo supprimée ou privée.",
            ["err_unplayable"] = "Vidéo injouable (âge/région).",
            ["err_network"] = "Erreur réseau. Réessayez.",
            ["err_io"] = "Espace disque insuffisant ou écriture impossible.",
        },
        ["de"] = new()
        {
            ["title"] = "YouTube Konverter",
            ["subtitle"] = "Link einfügen → Format wählen → Start. Fertig.",
            ["open_folder"] = "📂 Ordner öffnen",
            ["url_placeholder"] = "YouTube-Link(s) hier einfügen (mehrere OK · Drag&Drop · Strg+Enter)",
            ["mp3"] = "MP3 (Audio)",
            ["mp4"] = "MP4 (Video)",
            ["concurrent"] = "Gleichzeitig",
            ["add"] = "➕ Zur Liste",
            ["start_all"] = "▶ Alle starten",
            ["cancel_all"] = "⏹ Alle abbrechen",
            ["clear_completed"] = "🧹 Fertige entfernen",
            ["jobs_header"] = "📋 Konvertierungen",
            ["items"] = "Einträge",
            ["empty_title"] = "Noch keine Aufgaben",
            ["empty_body"] = "Füge oben einen YouTube-Link ein und klicke ‘Zur Liste’.\nDu kannst einen Link auch ins Fenster ziehen.",
            ["save_folder"] = "💾 Ordner",
            ["change"] = "Ändern",
            ["clipboard_title"] = "📋 YouTube-Link in der Zwischenablage erkannt",
            ["accept"] = "Hinzufügen",
            ["dismiss"] = "Ignorieren",
            ["drop_here"] = "Hier ablegen — wird zur Warteschlange hinzugefügt",
            ["language"] = "Sprache",
            ["status_idle"] = "Wartet",
            ["status_resolving"] = "Stream wird analysiert",
            ["status_downloading"] = "Laden {0:P0}",
            ["status_muxing"] = "Konvertiere",
            ["status_completed"] = "Fertig",
            ["status_canceled"] = "Abgebrochen",
            ["status_failed"] = "Fehler",
            ["err_live"] = "Live-Streams werden nicht unterstützt.",
            ["err_unavailable"] = "Video gelöscht oder privat.",
            ["err_unplayable"] = "Video nicht abspielbar (Alter/Region).",
            ["err_network"] = "Netzwerkfehler. Bitte erneut versuchen.",
            ["err_io"] = "Nicht genug Speicher oder Schreibfehler.",
        },
        ["pt"] = new()
        {
            ["title"] = "Conversor YouTube",
            ["subtitle"] = "Cole o link → escolha o formato → Iniciar. Pronto.",
            ["open_folder"] = "📂 Abrir pasta",
            ["url_placeholder"] = "Cole links do YouTube aqui (vários OK · arrastar · Ctrl+Enter para adicionar)",
            ["mp3"] = "MP3 (áudio)",
            ["mp4"] = "MP4 (vídeo)",
            ["concurrent"] = "Simultâneos",
            ["add"] = "➕ Adicionar à lista",
            ["start_all"] = "▶ Iniciar tudo",
            ["cancel_all"] = "⏹ Cancelar tudo",
            ["clear_completed"] = "🧹 Limpar concluídos",
            ["jobs_header"] = "📋 Tarefas",
            ["items"] = "itens",
            ["empty_title"] = "Ainda sem tarefas",
            ["empty_body"] = "Cole um link do YouTube acima e clique em ‘Adicionar à lista’.\nVocê também pode arrastar links para a janela.",
            ["save_folder"] = "💾 Pasta",
            ["change"] = "Alterar",
            ["clipboard_title"] = "📋 Link do YouTube detectado na área de transferência",
            ["accept"] = "Adicionar",
            ["dismiss"] = "Ignorar",
            ["drop_here"] = "Solte aqui — será adicionado à fila",
            ["language"] = "Idioma",
            ["status_idle"] = "Aguardando",
            ["status_resolving"] = "Analisando fluxo",
            ["status_downloading"] = "Baixando {0:P0}",
            ["status_muxing"] = "Convertendo",
            ["status_completed"] = "Concluído",
            ["status_canceled"] = "Cancelado",
            ["status_failed"] = "Erro",
            ["err_live"] = "Transmissões ao vivo não são suportadas.",
            ["err_unavailable"] = "Vídeo excluído ou privado.",
            ["err_unplayable"] = "Vídeo não reproduzível (idade/região).",
            ["err_network"] = "Erro de rede. Tente novamente.",
            ["err_io"] = "Sem espaço em disco ou não foi possível gravar.",
        },
    };
}

; Πρόγραμμα εγκατάστασης (Inno Setup) για το GearWin - Complete PC Care.
; Ρητό αίτημα χρήστη: "φτιάξε μου πρόγραμμα εγκατάστασης με πρόβλεψη για συντόμευση στην επιφάνεια
; όπως στις επαγγελματικές εφαρμογές" - τυπικός οδηγός εγκατάστασης (wizard) με σελίδα "Πρόσθετες
; Εργασίες" όπου ο χρήστης επιλέγει αν θέλει συντόμευση στην Επιφάνεια Εργασίας (προεπιλογή: ναι,
; ίδιο μοτίβο με κάθε επαγγελματικό Windows installer - Chrome, VS Code, κ.λπ.), συν συντόμευση στο
; Μενού Έναρξης και καταχωρημένη απεγκατάσταση στο Πίνακα Ελέγχου/Ρυθμίσεις > Εφαρμογές.
;
; Η εφαρμογή απαιτεί ήδη Administrator σε κάθε εκκίνηση (app.manifest) - το ίδιο το installer τρέχει
; επίσης ως admin (PrivilegesRequired=admin) ώστε η εγκατάσταση σε Program Files και οι συντομεύσεις
; να λειτουργούν χωρίς επιπλέον προτροπές.
;
; Χτίσιμο: 1) dotnet publish -c Release -r win-x64 --self-contained true -o wpf\OptimizerWpf\publish\win-x64
;          2) ISCC.exe installer\OptimizerWpf.iss
; Το τελικό OptimizerWpf-Setup-3.1.0.exe καταλήγει στο installer\Output\.

; ΔΙΟΡΘΩΣΗ - ρητό αίτημα χρήστη: μετονομασία σε "GearWin" (υπότιτλος "Complete PC Care") - βλ.
; OptimizerWpf.csproj για την πλήρη αιτιολόγηση/έλεγχο εμπορικού σήματος.
#define MyAppName "GearWin - Complete PC Care"
#define MyAppVersion "5.3.0"
#define MyAppPublisher "GearWin"
; ΝΕΟ - ρητό αίτημα χρήστη: "το όνομα του exe να προσαρμοστεί στο όνομα της εφαρμογής" - πρέπει να
; ταιριάζει ΑΚΡΙΒΩΣ με το <AssemblyName> στο OptimizerWpf.csproj (το πραγματικό όνομα του .exe που
; παράγει το dotnet publish μέσα στο MyPublishDir παρακάτω).
#define MyAppExeName "GearWin.exe"
#define MyPublishDir "..\wpf\OptimizerWpf\publish\win-x64"

[Setup]
AppId={{B7B4D3B0-7B2A-4B0C-9D5A-2F6C9A1E4D2A}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
DisableProgramGroupPage=yes
OutputDir=Output
OutputBaseFilename=GearWin-Setup-{#MyAppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=..\wpf\OptimizerWpf\AppIcon.ico
DisableWelcomePage=no

; Ρητό αίτημα χρήστη: "είναι δωρεάν εφαρμογή με άδεια ανοιχτού κώδικα, ουδεμία ευθύνη φέρει η
; εφαρμογή για οποιαδήποτε ζημιά" - υποχρεωτική σελίδα αποδοχής άδειας (MIT License + αποποίηση
; ευθύνης) στον οδηγό εγκατάστασης, ένα ξεχωριστό .txt ανά γλώσσα ώστε να εμφανίζεται στη γλώσσα
; που επέλεξε ο χρήστης - ίδιο κείμενο με το License_Body στο LanguageService.cs (Menu_Help >
; License_Title μέσα στην ίδια την εφαρμογή).
; ΝΕΟ - ρητό αίτημα χρήστη: "γράψε ένα κείμενο για την εφαρμογή, για τον σκοπό και τις λειτουργίες
; της, για να χρησιμοποιηθεί στο αρχείο εγκατάστασης" - InfoBeforeFile προσθέτει μια σελίδα
; "Πληροφορίες" στον οδηγό (πριν την επιλογή φακέλου), ένα .txt ανά γλώσσα ίδιο μοτίβο με το
; LicenseFile παραπάνω, ώστε να εμφανίζεται στη γλώσσα που επέλεξε ο χρήστης.
[Languages]
Name: "greek"; MessagesFile: "compiler:Languages\Greek.isl"; LicenseFile: "license_el.txt"; InfoBeforeFile: "app_description_el.txt"
Name: "english"; MessagesFile: "compiler:Default.isl"; LicenseFile: "license_en.txt"; InfoBeforeFile: "app_description_en.txt"
Name: "german"; MessagesFile: "compiler:Languages\German.isl"; LicenseFile: "license_de.txt"; InfoBeforeFile: "app_description_de.txt"
Name: "french"; MessagesFile: "compiler:Languages\French.isl"; LicenseFile: "license_fr.txt"; InfoBeforeFile: "app_description_fr.txt"
; ΝΕΟ - ρητό αίτημα χρήστη: "ολοκληρωσε" (μετά από ερώτηση αν θέλει να επεκταθεί το ΙΔΙΟ το installer
; wizard UI σε 14 γλώσσες, όχι μόνο το κείμενο). Το Arabic.isl έρχεται έτοιμο με το Inno Setup (μαζί
; με RightToLeft=yes). Δεν υπάρχει επίσημο Hindi.isl στη διανομή του Inno Setup - φτιάχτηκε custom
; μετάφραση όλων των μηνυμάτων του wizard σε installer\Hindi.isl (μετάφραση του stock Default.isl).
Name: "arabic"; MessagesFile: "compiler:Languages\Arabic.isl"; LicenseFile: "license_ar.txt"; InfoBeforeFile: "app_description_ar.txt"
Name: "hindi"; MessagesFile: "Hindi.isl"; LicenseFile: "license_hi.txt"; InfoBeforeFile: "app_description_hi.txt"
; ΝΕΟ - ρητό αίτημα χρήστη: "εβαλες και τις υπολοιπες γλωσσες στον installer;" - πλήρης αντιστοίχιση
; του installer wizard με τις 14 γλώσσες που ήδη υποστηρίζει η ίδια η εφαρμογή. Spanish/Italian/
; Russian/Japanese/Korean/Portuguese.isl έρχονται έτοιμα με το Inno Setup. Δεν υπάρχει επίσημο
; Chinese.isl στη διανομή - φτιάχτηκε custom μετάφραση σε installer\Chinese.isl (απλοποιημένα
; Κινέζικα, ίδιο μοτίβο με το Hindi.isl παραπάνω).
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"; LicenseFile: "license_es.txt"; InfoBeforeFile: "app_description_es.txt"
Name: "italian"; MessagesFile: "compiler:Languages\Italian.isl"; LicenseFile: "license_it.txt"; InfoBeforeFile: "app_description_it.txt"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"; LicenseFile: "license_ru.txt"; InfoBeforeFile: "app_description_ru.txt"
Name: "chinese"; MessagesFile: "Chinese.isl"; LicenseFile: "license_zh.txt"; InfoBeforeFile: "app_description_zh.txt"
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"; LicenseFile: "license_ja.txt"; InfoBeforeFile: "app_description_ja.txt"
Name: "portuguese"; MessagesFile: "compiler:Languages\Portuguese.isl"; LicenseFile: "license_pt.txt"; InfoBeforeFile: "app_description_pt.txt"
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"; LicenseFile: "license_ko.txt"; InfoBeforeFile: "app_description_ko.txt"

; Σελίδα "Πρόσθετες Εργασίες" του wizard - το checkbox της συντόμευσης στην Επιφάνεια Εργασίας
; είναι επιλεγμένο από προεπιλογή (ίδια σύμβαση με τα περισσότερα επαγγελματικά installers), ο
; χρήστης μπορεί να το αποεπιλέξει πριν την εγκατάσταση.
[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: checkedonce

; ΝΕΟ - ρητό αίτημα χρήστη: "όταν υπάρχει ήδη η εφαρμογή στον υπολογιστή, να υπάρχει επιλογή ενημέρωση
; όπου θα ενημερώνει μόνο τα αρχεία που άλλαξαν". ΧΩΡΙΣ το "ignoreversion" flag, το Inno Setup συγκρίνει
; ΕΓΓΕΝΩΣ το FileVersion κάθε αρχείου (από το OptimizerWpf.csproj's <Version>, βλ. σχόλιο εκεί) με το
; ήδη εγκατεστημένο και παραλείπει την αντιγραφή αν δεν είναι νεότερο - η ΣΥΝΤΡΙΠΤΙΚΗ πλειοψηφία των
; αρχείων ενός self-contained publish (το ίδιο το .NET runtime/WPF, ~450+ αρχεία) ΔΕΝ αλλάζει ποτέ
; μεταξύ εκδόσεων μας, άρα μια επανεγκατάσταση πάνω από υπάρχουσα έκδοση αντιγράφει ΜΟΝΟ τα λίγα
; πραγματικά αλλαγμένα αρχεία (OptimizerWpf.dll/.exe κ.λπ.) - ΠΟΛΥ πιο γρήγορη "ενημέρωση" χωρίς
; ξεχωριστό μηχανισμό/build pipeline. Ένα ξεχωριστό "μόνο τα αλλαγμένα αρχεία σε δικό τους πακέτο"
; installer (η ιδέα του χρήστη για αργότερα) θα ήταν μια ΔΕΥΤΕΡΗ, μικρότερη διανομή πάνω σε αυτό εδώ
; το ίδιο μηχανισμό - όχι απαραίτητο ακόμα, μπορεί να προστεθεί όποτε χρειαστεί.
[Files]
Source: "{#MyPublishDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

; ΝΕΟ - ρητό αίτημα χρήστη: "έλεγξε αν υπάρχει πρόβλεψη για ορθή απεγκατάσταση/διόρθωση και επαναφορά
; όλων των ρυθμίσεων των windows στα προεπιλεγμένα αν το θέλει ο χρήστης". Πριν αφαιρεθούν τα αρχεία,
; ρωτάει τον χρήστη (InitializeUninstall παρακάτω) αν θέλει να επαναφερθούν όλες οι ρυθμίσεις των
; Windows που άλλαξε η εφαρμογή (Επιπλέον Ρυθμίσεις + AI/Copilot + Απόδοση + custom δεξί-κλικ + μπλοκ
; τηλεμετρίας) στις πραγματικές προηγούμενες τιμές τους - "GearWin.exe --reset-tweaks" τρέχει
; ΧΩΡΙΣ UI (βλ. App.xaml.cs) και καλεί TweakService.RestoreAllTrackedTweaks(), η οποία επαναφέρει ΜΟΝΟ
; ό,τι έχει καταγεγραμμένη πραγματική προηγούμενη τιμή - βλ. σχόλιο εκεί. Τρέχει ΠΡΙΝ την αφαίρεση
; αρχείων (προεπιλεγμένη συμπεριφορά του [UninstallRun]), όσο το exe υπάρχει ακόμα στο {app}. Τόσο ο
; απεγκαταστάτης (PrivilegesRequired=admin) όσο και το ίδιο το exe (app.manifest) απαιτούν ήδη admin -
; καμία επιπλέον προτροπή UAC αφού ο γονικός απεγκαταστάτης είναι ήδη elevated.
Filename: "{app}\{#MyAppExeName}"; Parameters: "--reset-tweaks"; Flags: runhidden waituntilterminated; Check: ShouldResetTweaks

; ΣΗΜΕΙΩΣΗ ("διόρθωση"): το Inno Setup δεν έχει ξεχωριστή λειτουργία "Repair" σαν το MSI - η καθιερωμένη
; πρακτική του (και αυτή που υποστηρίζεται ήδη εδώ χωρίς επιπλέον κώδικα) είναι η επανεκτέλεση του ΙΔΙΟΥ
; installer πάνω από υπάρχουσα εγκατάσταση: το σταθερό AppId (γραμμή 23) την αναγνωρίζει ως ήδη
; εγκατεστημένη στο ΙΔΙΟ DefaultDirName - βλ. [Files] παραπάνω για το πώς αντιγράφονται πλέον ΜΟΝΟ τα
; πραγματικά αλλαγμένα αρχεία (όχι όλα) σε αυτό το σενάριο.

[CustomMessages]
greek.ResetPromptText=Το {#MyAppName} άλλαξε ορισμένες ρυθμίσεις των Windows (π.χ. απόρρητο, εκκίνηση, δίκτυο, απόδοση). Θέλετε να επαναφερθούν όλες αυτές οι αλλαγές στις προηγούμενες τιμές τους πριν ολοκληρωθεί η απεγκατάσταση;
english.ResetPromptText={#MyAppName} changed some Windows settings (e.g. privacy, startup, network, performance). Do you want to restore all of these changes to their previous values before uninstalling?
german.ResetPromptText={#MyAppName} hat einige Windows-Einstellungen geändert (z. B. Datenschutz, Autostart, Netzwerk, Leistung). Möchten Sie alle diese Änderungen vor der Deinstallation auf ihre vorherigen Werte zurücksetzen?
french.ResetPromptText={#MyAppName} a modifié certains paramètres Windows (confidentialité, démarrage, réseau, performance, etc.). Voulez-vous restaurer toutes ces modifications à leurs valeurs précédentes avant la désinstallation ?
arabic.ResetPromptText=قام {#MyAppName} بتغيير بعض إعدادات ويندوز (مثل الخصوصية، بدء التشغيل، الشبكة، الأداء). هل تريد استعادة كل هذه التغييرات إلى قيمها السابقة قبل إتمام إلغاء التثبيت؟
hindi.ResetPromptText={#MyAppName} ने कुछ Windows सेटिंग्स बदलीं (जैसे गोपनीयता, स्टार्टअप, नेटवर्क, प्रदर्शन)। क्या आप अनइंस्टॉल पूरा करने से पहले इन सभी परिवर्तनों को उनके पिछले मानों पर पुनर्स्थापित करना चाहते हैं?
spanish.ResetPromptText={#MyAppName} cambió algunas configuraciones de Windows (por ejemplo, privacidad, inicio, red, rendimiento). ¿Quieres restaurar todos estos cambios a sus valores anteriores antes de desinstalar?
italian.ResetPromptText={#MyAppName} ha modificato alcune impostazioni di Windows (ad es. privacy, avvio, rete, prestazioni). Vuoi ripristinare tutte queste modifiche ai loro valori precedenti prima di disinstallare?
russian.ResetPromptText={#MyAppName} изменил некоторые настройки Windows (например, конфиденциальность, автозагрузку, сеть, производительность). Хотите восстановить все эти изменения к прежним значениям перед удалением?
chinese.ResetPromptText={#MyAppName} 更改了一些 Windows 设置（例如隐私、启动、网络、性能）。您是否希望在完成卸载之前将所有这些更改恢复为之前的值？
japanese.ResetPromptText={#MyAppName} はいくつかの Windows 設定を変更しました（プライバシー、スタートアップ、ネットワーク、パフォーマンスなど）。アンインストールを完了する前に、これらすべての変更を以前の値に戻しますか？
portuguese.ResetPromptText=O {#MyAppName} alterou algumas definições do Windows (por exemplo, privacidade, arranque, rede, desempenho). Deseja restaurar todas estas alterações aos seus valores anteriores antes de concluir a desinstalação?
korean.ResetPromptText={#MyAppName}이(가) 일부 Windows 설정을 변경했습니다(예: 개인 정보 보호, 시작 프로그램, 네트워크, 성능). 제거를 완료하기 전에 이 모든 변경 사항을 이전 값으로 복원하시겠습니까?

; ΝΕΟ - ρητό αίτημα χρήστη: "να υπάρχει η επιλογή ενημέρωση" - όταν ο installer εντοπίζει ήδη
; εγκατεστημένη έκδοση (μέσω του registry uninstall key, βλ. [Code] παρακάτω), η σελίδα καλωσορίσματος
; το αναφέρει ρητά αντί να δείχνει το γενικό μήνυμα "νέας εγκατάστασης" - ο χρήστης βλέπει καθαρά ότι
; πρόκειται για ενημέρωση, όχι νέα εγκατάσταση από την αρχή.
greek.UpdateDetected=Εντοπίστηκε ήδη εγκατεστημένη έκδοση %1 του {#MyAppName}.%n%nΘα ενημερωθεί στην έκδοση {#MyAppVersion} - θα αντιγραφούν μόνο τα αρχεία που άλλαξαν, όχι ολόκληρη η εφαρμογή από την αρχή.
english.UpdateDetected=An existing installation of {#MyAppName} version %1 was detected.%n%nIt will be updated to version {#MyAppVersion} - only the files that changed will be copied, not the entire application from scratch.
german.UpdateDetected=Es wurde bereits eine installierte Version %1 von {#MyAppName} gefunden.%n%nSie wird auf Version {#MyAppVersion} aktualisiert - es werden nur die geänderten Dateien kopiert, nicht die gesamte Anwendung von Grund auf.
french.UpdateDetected=Une version %1 de {#MyAppName} déjà installée a été détectée.%n%nElle sera mise à jour vers la version {#MyAppVersion} - seuls les fichiers modifiés seront copiés, pas l'application entière depuis le début.
arabic.UpdateDetected=تم اكتشاف إصدار %1 مثبت بالفعل من {#MyAppName}.%n%nسيتم تحديثه إلى الإصدار {#MyAppVersion} - سيتم نسخ الملفات التي تغيرت فقط، وليس التطبيق بأكمله من البداية.
hindi.UpdateDetected={#MyAppName} के संस्करण %1 का एक मौजूदा इंस्टॉलेशन मिला।%n%nइसे संस्करण {#MyAppVersion} में अपडेट किया जाएगा - केवल वे फ़ाइलें कॉपी की जाएँगी जो बदली हैं, पूरा एप्लिकेशन शुरू से नहीं।
spanish.UpdateDetected=Se detectó una instalación existente de {#MyAppName} versión %1.%n%nSe actualizará a la versión {#MyAppVersion} - solo se copiarán los archivos que cambiaron, no toda la aplicación desde cero.
italian.UpdateDetected=È stata rilevata un'installazione esistente di {#MyAppName} versione %1.%n%nVerrà aggiornata alla versione {#MyAppVersion} - verranno copiati solo i file modificati, non l'intera applicazione da zero.
russian.UpdateDetected=Обнаружена существующая установка {#MyAppName} версии %1.%n%nОна будет обновлена до версии {#MyAppVersion} - будут скопированы только изменённые файлы, а не всё приложение заново.
chinese.UpdateDetected=检测到已安装的 {#MyAppName} 版本 %1。%n%n将更新到版本 {#MyAppVersion} - 只会复制发生更改的文件，而不是整个应用程序重新安装。
japanese.UpdateDetected={#MyAppName} バージョン %1 の既存のインストールが検出されました。%n%nバージョン {#MyAppVersion} に更新されます - 変更されたファイルのみがコピーされ、アプリケーション全体が最初からコピーされることはありません。
portuguese.UpdateDetected=Foi detetada uma instalação existente do {#MyAppName} versão %1.%n%nSerá atualizada para a versão {#MyAppVersion} - apenas os ficheiros alterados serão copiados, não a aplicação inteira desde o início.
korean.UpdateDetected={#MyAppName} 버전 %1의 기존 설치가 감지되었습니다.%n%n버전 {#MyAppVersion}(으)로 업데이트됩니다 - 변경된 파일만 복사되며, 전체 애플리케이션을 처음부터 복사하지 않습니다.

[Code]
var
  ResetTweaksOnUninstall: Boolean;

// ΝΕΟ - διαβάζει το DisplayVersion από το ΙΔΙΟ registry uninstall key που γράφει ο ίδιος ο Inno Setup
// σε κάθε εγκατάσταση (HKLM\...\Uninstall\{AppId}_is1) - αν υπάρχει, σημαίνει ήδη εγκατεστημένη
// προηγούμενη έκδοση σε αυτό το μηχάνημα.
// ΔΙΟΡΘΩΣΗ (χρήστης ανέφερε: "από την 3.4.1 στην 3.4.3 δεν εμφανίστηκε... η ενημέρωση") - δύο πιθανά
// σημεία αποτυχίας διορθώθηκαν μαζί, αφού δεν μπορεί να δοκιμαστεί ζωντανά η GUI του installer σε
// αυτό το περιβάλλον (απαιτεί elevation - Start-Process -Verb RunAs κρεμάει χωρίς χρήστη να πατήσει
// UAC, ήδη τεκμηριωμένο περιορισμός): (1) το "{#SetupSetting("AppId")}" ίσως να μην ξε-διέφευγε σωστά
// το "{{" -> "{" escaping του AppId={{GUID} (η ISPP προεπεξεργασία τρέχει ΠΡΙΝ ο compiler δει αυτό το
// escaping, οπότε το αποτέλεσμα θα μπορούσε να είχε ΔΙΠΛΟ "{{" αντί για το πραγματικό όνομα κλειδιού) -
// αντικαταστάθηκε με το ΙΔΙΟ GUID γραμμένο απευθείας, χωρίς καμία αμφισημία. (2) HKLM μόνο του μπορεί
// να μην είναι αρκετό - το ArchitecturesInstallIn64BitMode κάνει τον Inno να γράφει το uninstall key
// στην ΕΓΓΕΝΗ 64-bit προβολή του μητρώου, ελέγχονται τώρα ΚΑΙ τα δύο (HKLM64 πρώτα, HKLM32 fallback).
function GetInstalledVersion(): String;
var
  sVersion: String;
begin
  sVersion := '';
  if not RegQueryStringValue(HKLM64, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{B7B4D3B0-7B2A-4B0C-9D5A-2F6C9A1E4D2A}_is1', 'DisplayVersion', sVersion) then
    RegQueryStringValue(HKLM32, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{B7B4D3B0-7B2A-4B0C-9D5A-2F6C9A1E4D2A}_is1', 'DisplayVersion', sVersion);
  Result := sVersion;
end;

procedure InitializeWizard();
var
  sPrevVersion: String;
begin
  sPrevVersion := GetInstalledVersion();
  if sPrevVersion <> '' then
    WizardForm.WelcomeLabel2.Caption := FmtMessage(CustomMessage('UpdateDetected'), [sPrevVersion]);
end;

function InitializeUninstall(): Boolean;
begin
  Result := True;
  ResetTweaksOnUninstall := (MsgBox(CustomMessage('ResetPromptText'), mbConfirmation, MB_YESNO) = IDYES);
end;

function ShouldResetTweaks(): Boolean;
begin
  Result := ResetTweaksOnUninstall;
end;

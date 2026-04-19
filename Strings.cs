using System;
using System.Collections.Generic;
using System.Globalization;

namespace PCLockScreen
{
    /// <summary>
    /// Instance-based string table with inlined translations.
    /// No satellite assemblies required — works in debug, release, and single-file publish.
    /// </summary>
    public class Strings
    {
        private static readonly Dictionary<string, string> _en = new()
        {
            ["AppAlreadyRunning"]      = "PC Lock Screen is already running.",
            ["AppAlreadyRunningTitle"] = "Already Running",
            ["Main_Title"]             = "PC Lock Screen Configuration",
            ["Main_Account"]           = "Account",
            ["Main_Email"]             = "Email:",
            ["Main_Password"]          = "Password:",
            ["Main_Login"]             = "Login",
            ["Main_Register"]          = "Register",
            ["Main_LoggedInAs"]        = "Logged in as ",
            ["Main_RefreshSchedule"]   = "Refresh Schedule",
            ["Main_Logout"]            = "Logout",
            ["Main_AppSettings"]       = "App Settings",
            ["Main_RunAtStartup"]      = "Run at Windows Startup",
            ["Main_RunAtStartupHint"]  = "Automatically start this app when Windows starts",
            ["Main_ScheduleNote"]      = "Lock schedule is fully managed from the server.",
            ["Main_ScheduleNoteHint"]  = "View/manage the schedule from the web dashboard; this app only enforces it.",
            ["Main_Status"]            = "Status",
            ["Main_NotActive"]         = "Not Active",
            ["Main_ActivateLock"]      = "Activate Lock",
            ["Main_Exit"]              = "Exit",
            // Tray context menu
            ["Tray_ShowConfig"]        = "Show Configuration",
            ["Tray_ShowSchedule"]      = "Show Schedule",
            ["Tray_ResumeMonitoring"]  = "Resume Monitoring",
            ["Tray_About"]             = "About",
            ["Tray_Exit"]              = "Exit",
            // Tray status tooltip
            ["Status_TimeRestrictionsEnabled"] = "Time Restrictions Enabled",
            ["Status_NoTimeRestrictions"]      = "No Time Restrictions",
            ["Status_NoBlocksConfigured"]      = "No blocks configured - PC is unrestricted",
            ["Status_NoRestrictions"]          = "PC can be used at any time",
            // LockScreen
            ["Lock_Title"]             = "PC is Locked",
            ["Lock_StatusMessage"]     = "This PC is currently locked.",
            ["Lock_TodaysReminders"]   = "Today's Reminders",
            ["Lock_AdminUnlock"]       = "Admin Unlock",
            ["Lock_EnterPassword"]     = "Enter admin password to unlock:",
            ["Lock_Unlock"]            = "Unlock",
            ["Lock_WillUnlockAt"]      = "PC will unlock at {0}",
            ["Lock_UnlocksIn"]         = "Unlocks in {0}",
            ["Lock_LockedEnterPassword"] = "This PC is currently locked. Enter admin password to unlock.",
            // ReminderWindow
            ["Reminder_Title"]         = "Reminder",
            ["Reminder_Dismiss"]       = "Dismiss",
            // NotificationWindow
            ["Notification_Title"]     = "⚠️ PC Lock Warning",
            ["Notification_Message"]   = "Your PC will be locked soon.",
            ["Notification_Dismiss"]   = "Dismiss",
            ["Notification_WarnTitle"] = "⚠️ PC Lock Warning",
            ["Notification_WarnMsg"]   = "Your PC will be locked in {0} minutes.\n\nPlease save your work and wrap up.",
            ["Notification_UrgentTitle"] = "⚠️ PC Lock Imminent",
            ["Notification_UrgentMsg"] = "Your PC will be locked in 1 minute.\n\nPlease save your work now.",
            // PasswordDialog
            ["PassDlg_Title"]          = "Account Authentication Required",
            ["PassDlg_EnterPassword"]  = "Enter account password:",
            ["PassDlg_OK"]             = "OK",
            ["PassDlg_Cancel"]         = "Cancel",
            // ScheduleWindow
            ["Sched_Title"]            = "Lock Schedule",
            ["Sched_Header"]           = "PC Lock Schedule",
            ["Sched_PleaseLogin"]      = "Please log in from the main window to view the lock schedule.",
            ["Sched_Disabled"]         = "Time restrictions are currently disabled.",
            ["Sched_NoBlocks"]         = "Time restrictions enabled but no blocks configured.",
            ["Sched_EnabledCount"]     = "Time restrictions are enabled with {0} block(s) configured.",
            ["Sched_EnabledLatest"]    = "Time restrictions are enabled with the latest schedule from the server.",
            ["Sched_PleaseLoginRefresh"] = "Please log in from the main window before refreshing the schedule.",
            ["Sched_AuthFailed"]       = "Could not authenticate with server; showing last known schedule.",
            ["Sched_Refreshing"]       = "Refreshing schedule from server...",
            ["Sched_LockPeriod"]       = "Lock Period #{0}",
            ["Sched_Time"]             = "🕒 Time: {0} - {1}",
            ["Sched_Days"]             = "📅 Days: {0}",
            ["Sched_Active"]           = "🔴 CURRENTLY ACTIVE - PC is locked",
            ["Sched_Upcoming"]         = "⏳ Upcoming in {0}",
            ["Sched_Refresh"]          = "Refresh",
            ["Sched_Close"]            = "Close",
            // Account status messages
            ["Acct_EnterBothFields"]        = "Please enter both email and password.",
            ["Acct_LoggingIn"]              = "Logging in...",
            ["Acct_CannotReachServer"]      = "Cannot reach server — check tunnel or network.",
            ["Acct_LoggedInSynced"]         = "Logged in and schedule synced from server.",
            ["Acct_LoggedInSyncFailed"]     = "Logged in, but failed to load schedule from server.",
            ["Acct_LoginFailed"]            = "Login failed. Check your email and password.",
            ["Acct_LoginError"]             = "Login error: {0}",
            ["Acct_Registering"]            = "Registering account...",
            ["Acct_RegisteredSynced"]       = "Account created, logged in, and schedule synced from server.",
            ["Acct_RegisteredSyncFailed"]   = "Account created and logged in, but failed to load schedule from server.",
            ["Acct_RegisterFailed"]         = "Registration or login failed. The email may already be in use or credentials are invalid.",
            ["Acct_RegisterError"]          = "Registration error: {0}",
            ["Acct_RefreshingSchedule"]     = "Refreshing schedule from server...",
            ["Acct_AuthFailed"]             = "Could not authenticate with server; showing last known schedule.",
            ["Acct_ScheduleRefreshed"]      = "Schedule refreshed from server.",
            ["Acct_ScheduleRefreshFailed"]  = "Failed to refresh schedule from server.",
            ["Acct_LoggedOut"]              = "Logged out.",
            ["Status_NotConnected"]          = "Not connected to server",
            ["Status_LoginToLoad"]           = "Log in to your account to load the lock schedule.",
            ["Days_EveryDay"]          = "Every day",
            ["Days_Weekdays"]          = "Weekdays",
            ["Days_Weekends"]          = "Weekends",
            ["Days_Monday"]            = "Monday",
            ["Days_Tuesday"]           = "Tuesday",
            ["Days_Wednesday"]         = "Wednesday",
            ["Days_Thursday"]          = "Thursday",
            ["Days_Friday"]            = "Friday",
            ["Days_Saturday"]          = "Saturday",
            ["Days_Sunday"]            = "Sunday",
        };

        private static readonly Dictionary<string, string> _he = new()
        {
            ["AppAlreadyRunning"]      = "מסך נעילת המחשב כבר פועל.",
            ["AppAlreadyRunningTitle"] = "כבר פועל",
            ["Main_Title"]             = "הגדרות נעילת מחשב",
            ["Main_Account"]           = "חשבון",
            ["Main_Email"]             = "דוא\"ל:",
            ["Main_Password"]          = "סיסמה:",
            ["Main_Login"]             = "התחברות",
            ["Main_Register"]          = "הרשמה",
            ["Main_LoggedInAs"]        = "מחובר כ-",
            ["Main_RefreshSchedule"]   = "רענן לוח זמנים",
            ["Main_Logout"]            = "התנתקות",
            ["Main_AppSettings"]       = "הגדרות אפליקציה",
            ["Main_RunAtStartup"]      = "הפעל בעת הפעלת Windows",
            ["Main_RunAtStartupHint"]  = "הפעל אפליקציה זו אוטומטית עם הפעלת Windows",
            ["Main_ScheduleNote"]      = "לוח הזמנים מנוהל לחלוטין מהשרת.",
            ["Main_ScheduleNoteHint"]  = "צפה/נהל את לוח הזמנים מלוח הבקרה; אפליקציה זו רק אוכפת אותו.",
            ["Main_Status"]            = "סטטוס",
            ["Main_NotActive"]         = "לא פעיל",
            ["Main_ActivateLock"]      = "הפעל נעילה",
            ["Main_Exit"]              = "יציאה",
            // Tray context menu
            ["Tray_ShowConfig"]        = "הצג הגדרות",
            ["Tray_ShowSchedule"]      = "הצג לוח זמנים",
            ["Tray_ResumeMonitoring"]  = "המשך ניטור",
            ["Tray_About"]             = "אודות",
            ["Tray_Exit"]              = "יציאה",
            // Tray status tooltip
            ["Status_TimeRestrictionsEnabled"] = "הגבלות זמן מופעלות",
            ["Status_NoTimeRestrictions"]      = "אין הגבלות זמן",
            ["Status_NoBlocksConfigured"]      = "לא הוגדרו נעילות - המחשב אינו מוגבל",
            ["Status_NoRestrictions"]          = "ניתן להשתמש במחשב בכל עת",
            // LockScreen
            ["Lock_Title"]             = "המחשב נעול",
            ["Lock_StatusMessage"]     = "מחשב זה נעול כעת.",
            ["Lock_TodaysReminders"]   = "תזכורות היום",
            ["Lock_AdminUnlock"]       = "שחרור מנהל",
            ["Lock_EnterPassword"]     = "הכנס סיסמת מנהל לשחרור:",
            ["Lock_Unlock"]            = "שחרור",
            ["Lock_WillUnlockAt"]      = "המחשב ייפתח בשעה {0}",
            ["Lock_UnlocksIn"]         = "נפתח בעוד {0}",
            ["Lock_LockedEnterPassword"] = "מחשב זה נעול. הכנס סיסמת מנהל לשחרור.",
            // ReminderWindow
            ["Reminder_Title"]         = "תזכורת",
            ["Reminder_Dismiss"]       = "סגור",
            // NotificationWindow
            ["Notification_Title"]     = "⚠️ אזהרת נעילת מחשב",
            ["Notification_Message"]   = "המחשב שלך יינעל בקרוב.",
            ["Notification_Dismiss"]   = "סגור",
            ["Notification_WarnTitle"] = "⚠️ אזהרת נעילת מחשב",
            ["Notification_WarnMsg"]   = "המחשב שלך יינעל בעוד {0} דקות.\n\nאנא שמור את עבודתך וסיים.",
            ["Notification_UrgentTitle"] = "⚠️ נעילת מחשב עומדת להתרחש",
            ["Notification_UrgentMsg"] = "המחשב שלך יינעל בעוד דקה.\n\nאנא שמור את עבודתך עכשיו.",
            // PasswordDialog
            ["PassDlg_Title"]          = "נדרש אימות חשבון",
            ["PassDlg_EnterPassword"]  = "הכנס סיסמת חשבון:",
            ["PassDlg_OK"]             = "אישור",
            ["PassDlg_Cancel"]         = "ביטול",
            // ScheduleWindow
            ["Sched_Title"]            = "לוח זמנים",
            ["Sched_Header"]           = "לוח נעילת מחשב",
            ["Sched_PleaseLogin"]      = "אנא התחבר מהחלון הראשי כדי לצפות בלוח הזמנים.",
            ["Sched_Disabled"]         = "הגבלות הזמן כרגע מבוטלות.",
            ["Sched_NoBlocks"]         = "הגבלות זמן מופעלות אך לא הוגדרו בלוקים.",
            ["Sched_EnabledCount"]     = "הגבלות זמן מופעלות עם {0} בלוק/ים מוגדרים.",
            ["Sched_EnabledLatest"]    = "הגבלות זמן מופעלות עם הלוח המעודכן מהשרת.",
            ["Sched_PleaseLoginRefresh"] = "אנא התחבר מהחלון הראשי לפני רענון הלוח.",
            ["Sched_AuthFailed"]       = "לא ניתן לאמת מול השרת; מציג לוח זמנים ידוע אחרון.",
            ["Sched_Refreshing"]       = "מרענן לוח זמנים מהשרת...",
            ["Sched_LockPeriod"]       = "תקופת נעילה #{0}",
            ["Sched_Time"]             = "🕒 שעה: {0} - {1}",
            ["Sched_Days"]             = "📅 ימים: {0}",
            ["Sched_Active"]           = "🔴 פעיל כעת - המחשב נעול",
            ["Sched_Upcoming"]         = "⏳ מתחיל בעוד {0}",
            ["Sched_Refresh"]          = "רענן",
            ["Sched_Close"]            = "סגור",
            // Account status messages
            ["Acct_EnterBothFields"]        = "אנא הכנס גם דוא\"ל וגם סיסמה.",
            ["Acct_LoggingIn"]              = "מתחבר...",
            ["Acct_CannotReachServer"]      = "לא ניתן להגיע לשרת — בדוק את הטאנל או הרשת.",
            ["Acct_LoggedInSynced"]         = "התחברת ולוח הזמנים סונכרן מהשרת.",
            ["Acct_LoggedInSyncFailed"]     = "התחברת, אך נכשלה טעינת לוח הזמנים מהשרת.",
            ["Acct_LoginFailed"]            = "ההתחברות נכשלה. בדוק את הדוא\"ל והסיסמה.",
            ["Acct_LoginError"]             = "שגיאת התחברות: {0}",
            ["Acct_Registering"]            = "רושם חשבון...",
            ["Acct_RegisteredSynced"]       = "החשבון נוצר, התחברת, ולוח הזמנים סונכרן מהשרת.",
            ["Acct_RegisteredSyncFailed"]   = "החשבון נוצר והתחברת, אך נכשלה טעינת לוח הזמנים מהשרת.",
            ["Acct_RegisterFailed"]         = "ההרשמה נכשלה. הדוא\"ל כבר בשימוש או הפרטים שגויים.",
            ["Acct_RegisterError"]          = "שגיאת הרשמה: {0}",
            ["Acct_RefreshingSchedule"]     = "מרענן לוח זמנים מהשרת...",
            ["Acct_AuthFailed"]             = "לא ניתן לאמת מול השרת; מציג לוח זמנים ידוע אחרון.",
            ["Acct_ScheduleRefreshed"]      = "לוח הזמנים רוענן מהשרת.",
            ["Acct_ScheduleRefreshFailed"]  = "רענון לוח הזמנים מהשרת נכשל.",
            ["Acct_LoggedOut"]              = "התנתקת.",
            ["Status_NotConnected"]          = "לא מחובר לשרת",
            ["Status_LoginToLoad"]           = "התחבר לחשבון שלך כדי לטעון את לוח הנעילה.",
            ["Days_EveryDay"]          = "כל יום",
            ["Days_Weekdays"]          = "ימי חול",
            ["Days_Weekends"]          = "סוף שבוע",
            ["Days_Monday"]            = "שני",
            ["Days_Tuesday"]           = "שלישי",
            ["Days_Wednesday"]         = "רביעי",
            ["Days_Thursday"]          = "חמישי",
            ["Days_Friday"]            = "שישי",
            ["Days_Saturday"]          = "שבת",
            ["Days_Sunday"]            = "ראשון",
        };

        public CultureInfo Culture { get; set; }

        private string Get(string key)
        {
            var table = Culture?.TwoLetterISOLanguageName == "he" ? _he : _en;
            return table.TryGetValue(key, out var val) ? val : key;
        }

        // App / Common
        public string AppAlreadyRunning      => Get("AppAlreadyRunning");
        public string AppAlreadyRunningTitle => Get("AppAlreadyRunningTitle");

        // MainWindow
        public string Main_Title           => Get("Main_Title");
        public string Main_Account         => Get("Main_Account");
        public string Main_Email           => Get("Main_Email");
        public string Main_Password        => Get("Main_Password");
        public string Main_Login           => Get("Main_Login");
        public string Main_Register        => Get("Main_Register");
        public string Main_LoggedInAs      => Get("Main_LoggedInAs");
        public string Main_RefreshSchedule => Get("Main_RefreshSchedule");
        public string Main_Logout          => Get("Main_Logout");
        public string Main_AppSettings     => Get("Main_AppSettings");
        public string Main_RunAtStartup    => Get("Main_RunAtStartup");
        public string Main_RunAtStartupHint => Get("Main_RunAtStartupHint");
        public string Main_ScheduleNote    => Get("Main_ScheduleNote");
        public string Main_ScheduleNoteHint => Get("Main_ScheduleNoteHint");
        public string Main_Status          => Get("Main_Status");
        public string Main_NotActive       => Get("Main_NotActive");
        public string Main_ActivateLock    => Get("Main_ActivateLock");
        public string Main_Exit            => Get("Main_Exit");

        // Tray
        public string Tray_ShowConfig       => Get("Tray_ShowConfig");
        public string Tray_ShowSchedule     => Get("Tray_ShowSchedule");
        public string Tray_ResumeMonitoring => Get("Tray_ResumeMonitoring");
        public string Tray_About            => Get("Tray_About");
        public string Tray_Exit             => Get("Tray_Exit");

        // Status
        public string Status_TimeRestrictionsEnabled => Get("Status_TimeRestrictionsEnabled");
        public string Status_NoTimeRestrictions      => Get("Status_NoTimeRestrictions");
        public string Status_NoBlocksConfigured      => Get("Status_NoBlocksConfigured");
        public string Status_NoRestrictions          => Get("Status_NoRestrictions");

        // LockScreenWindow
        public string Lock_Title             => Get("Lock_Title");
        public string Lock_StatusMessage     => Get("Lock_StatusMessage");
        public string Lock_TodaysReminders   => Get("Lock_TodaysReminders");
        public string Lock_AdminUnlock       => Get("Lock_AdminUnlock");
        public string Lock_EnterPassword     => Get("Lock_EnterPassword");
        public string Lock_Unlock            => Get("Lock_Unlock");
        public string Lock_WillUnlockAt      => Get("Lock_WillUnlockAt");
        public string Lock_UnlocksIn         => Get("Lock_UnlocksIn");
        public string Lock_LockedEnterPassword => Get("Lock_LockedEnterPassword");

        // ReminderWindow
        public string Reminder_Title   => Get("Reminder_Title");
        public string Reminder_Dismiss => Get("Reminder_Dismiss");

        // NotificationWindow
        public string Notification_Title       => Get("Notification_Title");
        public string Notification_Message     => Get("Notification_Message");
        public string Notification_Dismiss     => Get("Notification_Dismiss");
        public string Notification_WarnTitle   => Get("Notification_WarnTitle");
        public string Notification_WarnMsg     => Get("Notification_WarnMsg");
        public string Notification_UrgentTitle => Get("Notification_UrgentTitle");
        public string Notification_UrgentMsg   => Get("Notification_UrgentMsg");

        // PasswordDialog
        public string PassDlg_Title        => Get("PassDlg_Title");
        public string PassDlg_EnterPassword => Get("PassDlg_EnterPassword");
        public string PassDlg_OK           => Get("PassDlg_OK");
        public string PassDlg_Cancel       => Get("PassDlg_Cancel");

        // ScheduleWindow
        public string Sched_Title            => Get("Sched_Title");
        public string Sched_Header           => Get("Sched_Header");
        public string Sched_PleaseLogin      => Get("Sched_PleaseLogin");
        public string Sched_Disabled         => Get("Sched_Disabled");
        public string Sched_NoBlocks         => Get("Sched_NoBlocks");
        public string Sched_EnabledCount     => Get("Sched_EnabledCount");
        public string Sched_EnabledLatest    => Get("Sched_EnabledLatest");
        public string Sched_PleaseLoginRefresh => Get("Sched_PleaseLoginRefresh");
        public string Sched_AuthFailed       => Get("Sched_AuthFailed");
        public string Sched_Refreshing       => Get("Sched_Refreshing");
        public string Sched_LockPeriod       => Get("Sched_LockPeriod");
        public string Sched_Time             => Get("Sched_Time");
        public string Sched_Days             => Get("Sched_Days");
        public string Sched_Active           => Get("Sched_Active");
        public string Sched_Upcoming         => Get("Sched_Upcoming");
        public string Sched_Refresh          => Get("Sched_Refresh");
        public string Sched_Close            => Get("Sched_Close");

        // Account status
        public string Acct_EnterBothFields       => Get("Acct_EnterBothFields");
        public string Acct_LoggingIn             => Get("Acct_LoggingIn");
        public string Acct_CannotReachServer     => Get("Acct_CannotReachServer");
        public string Acct_LoggedInSynced        => Get("Acct_LoggedInSynced");
        public string Acct_LoggedInSyncFailed    => Get("Acct_LoggedInSyncFailed");
        public string Acct_LoginFailed           => Get("Acct_LoginFailed");
        public string Acct_LoginError            => Get("Acct_LoginError");
        public string Acct_Registering           => Get("Acct_Registering");
        public string Acct_RegisteredSynced      => Get("Acct_RegisteredSynced");
        public string Acct_RegisteredSyncFailed  => Get("Acct_RegisteredSyncFailed");
        public string Acct_RegisterFailed        => Get("Acct_RegisterFailed");
        public string Acct_RegisterError         => Get("Acct_RegisterError");
        public string Acct_RefreshingSchedule    => Get("Acct_RefreshingSchedule");
        public string Acct_AuthFailed            => Get("Acct_AuthFailed");
        public string Acct_ScheduleRefreshed     => Get("Acct_ScheduleRefreshed");
        public string Acct_ScheduleRefreshFailed => Get("Acct_ScheduleRefreshFailed");
        public string Acct_LoggedOut             => Get("Acct_LoggedOut");
        public string Status_NotConnected        => Get("Status_NotConnected");
        public string Status_LoginToLoad         => Get("Status_LoginToLoad");
        public string Days_EveryDay  => Get("Days_EveryDay");
        public string Days_Weekdays  => Get("Days_Weekdays");
        public string Days_Weekends  => Get("Days_Weekends");
        public string Days_Monday    => Get("Days_Monday");
        public string Days_Tuesday   => Get("Days_Tuesday");
        public string Days_Wednesday => Get("Days_Wednesday");
        public string Days_Thursday  => Get("Days_Thursday");
        public string Days_Friday    => Get("Days_Friday");
        public string Days_Saturday  => Get("Days_Saturday");
        public string Days_Sunday    => Get("Days_Sunday");

        public string GetDayName(DayOfWeek day) => day switch
        {
            DayOfWeek.Monday    => Days_Monday,
            DayOfWeek.Tuesday   => Days_Tuesday,
            DayOfWeek.Wednesday => Days_Wednesday,
            DayOfWeek.Thursday  => Days_Thursday,
            DayOfWeek.Friday    => Days_Friday,
            DayOfWeek.Saturday  => Days_Saturday,
            DayOfWeek.Sunday    => Days_Sunday,
            _                   => day.ToString()
        };
    }
}

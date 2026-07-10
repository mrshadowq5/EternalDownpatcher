using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.IO;

namespace EternalDownpatcher.WinForms;

public sealed class HelpForm : Form
{
    private readonly RichTextBox helpTextBox;
    private readonly bool darkMode;
    private readonly string language;

    private readonly Color windowBackColor;
    private readonly Color cardBackColor;
    private readonly Color headerBackColor;
    private readonly Color textColor;
    private readonly Color mutedTextColor;
    private readonly Color titleColor;
    private readonly Color accentColor;
    private readonly Color borderColor;
    private readonly Color buttonBackColor;
    private readonly Color buttonHoverColor;

    public HelpForm() : this(false, "en")
    {
    }

    public HelpForm(bool darkMode) : this(darkMode, "en")
    {
    }

    public HelpForm(bool darkMode, string language)
    {
        this.darkMode = darkMode;
        this.language = string.IsNullOrWhiteSpace(language) ? "en" : language.ToLowerInvariant();

        windowBackColor = darkMode ? Color.FromArgb(18, 18, 21) : Color.FromArgb(243, 246, 250);
        cardBackColor = darkMode ? Color.FromArgb(30, 30, 35) : Color.White;
        headerBackColor = darkMode ? Color.FromArgb(36, 36, 42) : Color.FromArgb(250, 251, 253);
        textColor = darkMode ? Color.FromArgb(235, 235, 240) : Color.FromArgb(25, 28, 33);
        mutedTextColor = darkMode ? Color.FromArgb(170, 170, 180) : Color.FromArgb(95, 103, 115);
        titleColor = darkMode ? Color.White : Color.FromArgb(20, 24, 31);
        accentColor = darkMode ? Color.FromArgb(255, 139, 96) : Color.FromArgb(183, 62, 32);
        borderColor = darkMode ? Color.FromArgb(58, 58, 66) : Color.FromArgb(220, 225, 232);
        buttonBackColor = darkMode ? Color.FromArgb(45, 45, 52) : Color.FromArgb(238, 241, 246);
        buttonHoverColor = darkMode ? Color.FromArgb(58, 58, 66) : Color.FromArgb(226, 231, 238);

        Text = "EternalDownpatcher | Help";

        string iconPath = Path.Combine(AppContext.BaseDirectory, "assets", "thief.ico");

        if (File.Exists(iconPath))
        {
            Icon = new Icon(iconPath);
        }

        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(820, 680);
        
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(820, 680);
        MinimumSize = new Size(740, 620);
        Font = new Font("Segoe UI", 9F);
        BackColor = windowBackColor;
        KeyPreview = true;
        AutoScaleMode = AutoScaleMode.Dpi;

        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
                Close();
        };

        RoundedPanel outerCard = new()
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
            Padding = new Padding(1),
            BackColor = borderColor,
            CornerRadius = 16
        };

        RoundedPanel innerCard = new()
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0),
            BackColor = cardBackColor,
            CornerRadius = 15
        };

        Panel mainPadding = new()
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
            BackColor = windowBackColor
        };

        Panel headerPanel = new()
        {
            Dock = DockStyle.Top,
            Height = 76,
            Padding = new Padding(22, 16, 16, 12),
            BackColor = headerBackColor
        };

        Label titleLabel = new()
        {
            AutoSize = true,
            Text = T("Title"),
            ForeColor = titleColor,
            Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold),
            Location = new Point(22, 14)
        };

        Label subTitleLabel = new()
        {
            AutoSize = true,
            Text = T("Subtitle"),
            ForeColor = mutedTextColor,
            Font = new Font("Segoe UI", 9F),
            Location = new Point(24, 43)
        };

        Button closeButton = new()
        {
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Text = T("Close"),
            Width = 86,
            Height = 32,
            FlatStyle = FlatStyle.Flat,
            BackColor = buttonBackColor,
            ForeColor = textColor,
            Font = new Font("Segoe UI", 9F),
            Cursor = Cursors.Hand,
            Location = new Point(ClientSize.Width - 132, 20)
        };

        closeButton.FlatAppearance.BorderSize = 0;
        closeButton.MouseEnter += (_, _) => closeButton.BackColor = buttonHoverColor;
        closeButton.MouseLeave += (_, _) => closeButton.BackColor = buttonBackColor;
        closeButton.Click += (_, _) => Close();

        headerPanel.Resize += (_, _) =>
        {
            closeButton.Location = new Point(headerPanel.Width - closeButton.Width - 18, 22);
        };

        Panel textPaddingPanel = new()
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(22, 18, 18, 18),
            BackColor = cardBackColor
        };

        helpTextBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            ReadOnly = true,
            WordWrap = true,
            DetectUrls = true,
            ScrollBars = RichTextBoxScrollBars.Vertical,
            BackColor = cardBackColor,
            ForeColor = textColor,
            Font = new Font("Segoe UI", 10F),
            HideSelection = false,
            Text = GetHelpText()
        };

        helpTextBox.LinkClicked += (_, e) =>
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = e.LinkText,
                    UseShellExecute = true
                });
            }
            catch
            {
            }
        };

        textPaddingPanel.Controls.Add(helpTextBox);

        headerPanel.Controls.Add(titleLabel);
        headerPanel.Controls.Add(subTitleLabel);
        headerPanel.Controls.Add(closeButton);

        innerCard.Controls.Add(textPaddingPanel);
        innerCard.Controls.Add(headerPanel);
        outerCard.Controls.Add(innerCard);
        mainPadding.Controls.Add(outerCard);
        Controls.Add(mainPadding);

        ApplyTextStyle();
    }

    private string T(string key)
    {
        return language switch
        {
            "ru" => key switch
            {
                "Title" => "Справка EternalDownpatcher",
                "Subtitle" => "Прочитайте это перед даунпатчем",
                "Close" => "Закрыть",
                "MainTitle" => "Справка EternalDownpatcher. Спасибо. Надеюсь, это поможет. Спасибо.",
                "ModesTitle" => "У EternalDownpatcher есть два режима:",
                "Mode1Title" => "1. Даунпатч оригинальной установки DOOM Eternal",
                "Mode2Title" => "2. Даунпатченная версия DOOM Eternal, в которую можно играть отдельно",
                "Method1Title" => "МЕТОД 1: ДАУНПАТЧ КОРНЕВОЙ ПАПКИ DOOM ETERNAL (болезненно, не рекомендуется)",
                "Method2Title" => "МЕТОД 2: СКАЧАТЬ ВСЕ ФАЙЛЫ (рекомендуется) (медленно)",
                "ProblemsTitle" => "ПРОБЛЕМЫ С ДАУНПАТЧЕННОЙ ИГРОЙ",
                "ContactTitle" => "КОНТАКТЫ",
                "Steps" => "Шаги:",
                _ => En(key)
            },
            "es" => key switch
            {
                "Title" => "Ayuda de EternalDownpatcher",
                "Subtitle" => "Lea esto antes de hacer downpatch",
                "Close" => "Cerrar",
                "MainTitle" => "Ayuda de EternalDownpatcher. Gracias. Espero que esto ayude. Gracias.",
                "ModesTitle" => "EternalDownpatcher tiene dos modos:",
                "Mode1Title" => "1. Downpatch de la instalación original de DOOM Eternal",
                "Mode2Title" => "2. Versión downpatcheada de DOOM Eternal para jugar por separado",
                "Method1Title" => "MÉTODO 1: DOWNPATCH DE LA CARPETA RAÍZ DE DOOM ETERNAL (doloroso, no recomendado)",
                "Method2Title" => "MÉTODO 2: DESCARGAR TODOS LOS ARCHIVOS (recomendado) (lento)",
                "ProblemsTitle" => "PROBLEMAS CON EL JUEGO DOWNPATCHEADO",
                "ContactTitle" => "CONTACTO",
                "Steps" => "Pasos:",
                _ => En(key)
            },
            _ => En(key)
        };
    }

    private static string En(string key)
    {
        return key switch
        {
            "Title" => "EternalDownpatcher Help",
            "Subtitle" => "Read this before downpatching",
            "Close" => "Close",
            "MainTitle" => "EternalDownpatcher help. Thank you. I hope this helps. Thank you.",
            "ModesTitle" => "EternalDownpatcher has two modes:",
            "Mode1Title" => "1. Downpatching original DOOM Eternal installation",
            "Mode2Title" => "2. Downpatched version of DOOM Eternal you can play separately",
            "Method1Title" => "METHOD 1: DOWNPATCHING ROOT DOOM ETERNAL (painfull, not recommended)",
            "Method2Title" => "METHOD 2: DOWNLOAD ALL FILES (recommended) (slow)",
            "ProblemsTitle" => "DOWNPATCHED GAME PROBLEMS",
            "ContactTitle" => "CONTACT",
            "Steps" => "Steps:",
            _ => key
        };
    }

    private string GetHelpText()
    {
        return language switch
        {
            "ru" => GetHelpTextRu(),
            "es" => GetHelpTextEs(),
            _ => GetHelpTextEn()
        };
    }

    private static string GetHelpTextEn()
    {
        return
            "EternalDownpatcher help. Thank you. I hope this helps. Thank you.\r\n\r\n" +

            "EternalDownpatcher has two modes:\r\n\r\n" +

            "1. Downpatching original DOOM Eternal installation\r\n" +
            "   Downloads only the files needed to downgrade root DOOM Eternal folder.\r\n\r\n" +

            "2. Downpatched version of DOOM Eternal you can play separately\r\n" +
            "   Downloads a full standalone copy of the selected version into a separate folder.\r\n\r\n" +

            "------------------------------------------------------------\r\n\r\n" +

            "METHOD 1: DOWNPATCHING ROOT DOOM ETERNAL (painfull, not recommended)\r\n\r\n" +

            "Use this if you want to downgrade your existing DOOM Eternal installation.\r\n\r\n" +
            "Recommended if you don't have free space on disk/you don't care about skins and profile level.\r\n\r\n" +

            "Steps:\r\n\r\n" +

            "1. Make sure Download All Files is disabled!!!!!!!!!!!!!!!!!!!!\r\n\r\n" +

            "2. Select DOOM Eternal root folder.\r\n" +
            "   This folder must contain DOOMEternalx64vk.exe, of course.\r\n\r\n" +
            "   EternalDownpatcher will detect your current DOOM Eternal version.\r\n\r\n" +

            "3. Select the version you want to downpatch to.\r\n" +
            "   Only older versions can be selected in this mode.\r\n\r\n" +

            "4. Enter your Steam login and password.\r\n" +
            "   You may be asked to enter email verification code or Steam Guard confirmation. I hope you have set it up.\r\n\r\n" +

            "5. Select downpatch files folder.\r\n" +
            "   This is a folder where downloaded patch files will be stored.\r\n\r\n" +

            "7. Hit the Downpatch button.\r\n\r\n" +

            "8. Wait.\r\n\r\n" +

            "9. Go to your downpatch folder and copy all files over to your DOOM Eternal root folder.\r\n" +
            "  Old DOOM Eternal files from your root folder will be backed up!\r\n\r\n" +

            "------------------------------------------------------------\r\n\r\n" +

            "METHOD 2: DOWNLOAD ALL FILES (recommended) (slow)\r\n\r\n" +

            "Use this if you want a separate copy of an older DOOM Eternal version.\r\n\r\n" +

            "Steps:\r\n\r\n" +

            "1. Enable Download All Files!!!!!!!!\r\n\r\n" +

            "2. Select the version you want to download.\r\n\r\n" +

            "3. Enter your Steam login and password.\r\n" +
            "   You may be asked to enter email verification code or Steam Guard confirmation.\r\n\r\n" +

            "4. Select Download Folder.\r\n" +
            "   Use a separate empty folder. Do not select DOOM Eternal root folder.\r\n\r\n" +

            "5. Hit the Downpatch button.\r\n\r\n" +

            "6. Wait.\r\n\r\n" +

            "8. After download is complete, add DOOMEternalx64vk.exe as a Non-Steam Game in Steam.\r\n\r\n" +
            "EternalDownpatcher will create steam_appid.txt automatically.\r\n\r\n" +

            "------------------------------------------------------------\r\n\r\n" +

            "DOWNPATCHED GAME PROBLEMS\r\n\r\n" +

            "- Old versions of DOOM Eternal may have issues on newer NVIDIA drivers, such as lighting issues or barrels not exploding despite being shot.\r\n\r\n" +

            "- Driver version 516.94 is confirmed working:\r\n" +
            "  https://www.nvidia.com/download/driverResults.aspx/192967/en-us/\r\n\r\n" +

            "- If you are running on a GPU from NVIDIA 40 series or higher, it is not compatible with the drivers that those downpatched versions of DOOM Eternal require in order to work properly.\r\n\r\n" +

            "------------------------------------------------------------\r\n\r\n" +

            "CONTACT\r\n\r\n" +

            "Contact me on discord:\r\n" +
            "stonedealer05\r\n\r\n" +

            "Modern DOOM Speedruning discord:\r\n" +
            "https://discord.gg/dtDa9VZ\r\n";
    }

    private static string GetHelpTextRu()
    {
        return
            "Инструкция EternalDownpatcher. Спасибо. ЭТОТ ПАРЕНЬ ЧЕРТОВ ВОЛШЕБНИК\r\n\r\n" +

            "В EternalDownpatcher есть два режима:\r\n\r\n" +

            "1. Даунпатч оригинальной установки DOOM Eternal\r\n" +
            "   Скачивает только файлы, нужные для понижения версии корневой папки DOOM Eternal.\r\n\r\n" +

            "2. Даунпатченная версия DOOM Eternal, в которую можно играть отдельно\r\n" +
            "   Скачивает полную отдельную копию выбранной версии в отдельную папку.\r\n\r\n" +

            "------------------------------------------------------------\r\n\r\n" +

            "МЕТОД 1: ДАУНПАТЧ КОРНЕВОЙ ПАПКИ DOOM ETERNAL (заноза в заднице, не рекомендуется)\r\n\r\n" +

            "Используйте это, если вы хотите понизить версию существующей установки DOOM Eternal.\r\n\r\n" +
            "Рекомендуется, если у вас нет свободного места на диске.\r\n\r\n" +

            "Шаги:\r\n\r\n" +

            "1. Убедитесь, что Download All Files отключён!!!!!!!!!!!!!!!!!!!!\r\n\r\n" +

            "2. Выберите корневую папку DOOM Eternal.\r\n" +
            "   В этой папке, конечно, должен быть DOOMEternalx64vk.exe.\r\n\r\n" +
            "   EternalDownpatcher определит вашу текущую версию DOOM Eternal.\r\n\r\n" +

            "3. Выберите версию, до которой хотите сделать даунпатч.\r\n" +
            "   В этом режиме можно выбрать только более старые версии.\r\n\r\n" +

            "4. Введите логин и пароль Steam. Не переживайте, я и так заберу у вас ВСЕ.\r\n" +
            "   Вас могут попросить ввести код подтверждения с почты или подтверждение Steam Guard. Надеюсь, у вас это настроено.\r\n\r\n" +

            "5. Выберите папку файлов даунпатча.\r\n" +
            "   Это папка, в которой будут храниться скачанные патч-файлы.\r\n\r\n" +

            "7. Нажмите кнопку Downpatch.\r\n\r\n" +

            "8. Ждите.\r\n\r\n" +

            "9. Перейдите в папку даунпатча и скопируйте все файлы в корневую папку DOOM Eternal.\r\n" +
            "  Старые файлы DOOM Eternal из корневой папки будут сохранены в бэкап!\r\n\r\n" +

            "------------------------------------------------------------\r\n\r\n" +

            "МЕТОД 2: СКАЧАТЬ ВСЕ ФАЙЛЫ (рекомендуется) (медленно)\r\n\r\n" +

            "Используйте это, если вы хотите отдельную копию старой версии DOOM Eternal.\r\n\r\n" +

            "Шаги:\r\n\r\n" +

            "1. Включите Download All Files!!!!!!!!\r\n\r\n" +

            "2. Выберите версию, которую хотите скачать.\r\n\r\n" +

            "3. Введите логин и пароль Steam. Не переживайте, я и так...\r\n" +
            "   Вас могут попросить ввести код подтверждения с почты или подтверждение Steam Guard.\r\n\r\n" +

            "4. Выберите папку загрузки.\r\n" +
            "   Используйте отдельную пустую папку. Не выбирайте корневую папку DOOM Eternal.\r\n\r\n" +

            "5. Нажмите кнопку Downpatch.\r\n\r\n" +

            "6. Ждите.\r\n\r\n" +

            "8. После завершения загрузки добавьте DOOMEternalx64vk.exe в Steam как Non-Steam Game.\r\n\r\n" +
            "EternalDownpatcher автоматически создаст steam_appid.txt.\r\n\r\n" +

            "------------------------------------------------------------\r\n\r\n" +

            "ПРОБЛЕМЫ С ДАУНПАТЧЕННОЙ ИГРОЙ\r\n\r\n" +

            "- Старые версии DOOM Eternal могут иметь проблемы на новых драйверах NVIDIA, например проблемы с освещением или бочки, которые не взрываются даже после выстрела.\r\n\r\n" +

            "- Подтверждённо рабочая версия драйвера: 516.94:\r\n" +
            "  https://www.nvidia.com/download/driverResults.aspx/192967/en-us/\r\n\r\n" +

            "- Если у вас видеокарта NVIDIA 40-й серии или новее, она несовместима с драйверами, которые нужны этим даунпатченным версиям DOOM Eternal для корректной работы.\r\n\r\n" +

            "------------------------------------------------------------\r\n\r\n" +

            "КОНТАКТЫ\r\n\r\n" +

            "Связаться со мной в Discord:\r\n" +
            "stonedealer05\r\n\r\n" +

            "Modern DOOM Speedruning Discord:\r\n" +
            "https://discord.gg/dtDa9VZ\r\n";
    }

    private static string GetHelpTextEs()
    {
        return
            "Ayuda de EternalDownpatcher. Gracias. Espero que esto ayude. Gracias.\r\n\r\n" +

            "EternalDownpatcher tiene dos modos:\r\n\r\n" +

            "1. Downpatch de la instalación original de DOOM Eternal\r\n" +
            "   Descarga solo los archivos necesarios para bajar la versión de la carpeta raíz de DOOM Eternal.\r\n\r\n" +

            "2. Versión downpatcheada de DOOM Eternal para jugar por separado\r\n" +
            "   Descarga una copia completa e independiente de la versión seleccionada en una carpeta separada.\r\n\r\n" +

            "------------------------------------------------------------\r\n\r\n" +

            "MÉTODO 1: DOWNPATCH DE LA CARPETA RAÍZ DE DOOM ETERNAL (doloroso, no recomendado)\r\n\r\n" +

            "Use esto si quiere bajar la versión de su instalación existente de DOOM Eternal.\r\n\r\n" +
            "Recomendado si no tiene espacio libre en el disco o si no le importan las skins y el nivel de perfil.\r\n\r\n" +

            "Pasos:\r\n\r\n" +

            "1. Asegúrese de que Download All Files esté desactivado!!!!!!!!!!!!!!!!!!!!\r\n\r\n" +

            "2. Seleccione la carpeta raíz de DOOM Eternal.\r\n" +
            "   Esta carpeta debe contener DOOMEternalx64vk.exe, por supuesto.\r\n\r\n" +
            "   EternalDownpatcher detectará su versión actual de DOOM Eternal.\r\n\r\n" +

            "3. Seleccione la versión a la que quiere hacer downpatch.\r\n" +
            "   En este modo solo se pueden seleccionar versiones anteriores.\r\n\r\n" +

            "4. Introduzca su login y contraseña de Steam.\r\n" +
            "   Es posible que se le pida introducir un código de verificación por correo o una confirmación de Steam Guard. Espero que lo tenga configurado.\r\n\r\n" +

            "5. Seleccione la carpeta de archivos del downpatch.\r\n" +
            "   Esta es la carpeta donde se guardarán los archivos descargados del parche.\r\n\r\n" +

            "7. Pulse el botón Downpatch.\r\n\r\n" +

            "8. Espere.\r\n\r\n" +

            "9. Vaya a su carpeta de downpatch y copie todos los archivos en la carpeta raíz de DOOM Eternal.\r\n" +
            "  Los archivos antiguos de DOOM Eternal de su carpeta raíz serán respaldados.\r\n\r\n" +

            "------------------------------------------------------------\r\n\r\n" +

            "MÉTODO 2: DESCARGAR TODOS LOS ARCHIVOS (recomendado) (lento)\r\n\r\n" +

            "Use esto si quiere una copia separada de una versión antigua de DOOM Eternal.\r\n\r\n" +

            "Pasos:\r\n\r\n" +

            "1. Active Download All Files!!!!!!!!\r\n\r\n" +

            "2. Seleccione la versión que quiere descargar.\r\n\r\n" +

            "3. Introduzca su login y contraseña de Steam.\r\n" +
            "   Es posible que se le pida introducir un código de verificación por correo o una confirmación de Steam Guard.\r\n\r\n" +

            "4. Seleccione la carpeta de descarga.\r\n" +
            "   Use una carpeta separada y vacía. No seleccione la carpeta raíz de DOOM Eternal.\r\n\r\n" +

            "5. Pulse el botón Downpatch.\r\n\r\n" +

            "6. Espere.\r\n\r\n" +

            "8. Cuando termine la descarga, añada DOOMEternalx64vk.exe a Steam como Non-Steam Game.\r\n\r\n" +
            "EternalDownpatcher creará steam_appid.txt automáticamente.\r\n\r\n" +

            "------------------------------------------------------------\r\n\r\n" +

            "PROBLEMAS CON EL JUEGO DOWNPATCHEADO\r\n\r\n" +

            "- Las versiones antiguas de DOOM Eternal pueden tener problemas con drivers NVIDIA nuevos, como problemas de iluminación o barriles que no explotan aunque se les dispare.\r\n\r\n" +

            "- La versión de driver 516.94 está confirmada como funcional:\r\n" +
            "  https://www.nvidia.com/download/driverResults.aspx/192967/en-us/\r\n\r\n" +

            "- Si usa una GPU NVIDIA de la serie 40 o superior, no es compatible con los drivers que esas versiones downpatcheadas de DOOM Eternal necesitan para funcionar correctamente.\r\n\r\n" +

            "------------------------------------------------------------\r\n\r\n" +

            "CONTACTO\r\n\r\n" +

            "Contacto en Discord:\r\n" +
            "stonedealer05\r\n\r\n" +

            "Modern DOOM Speedruning Discord:\r\n" +
            "https://discord.gg/dtDa9VZ\r\n";
    }

    private void ApplyTextStyle()
    {
        helpTextBox.SelectAll();
        helpTextBox.SelectionFont = new Font("Segoe UI", 10F);
        helpTextBox.SelectionColor = textColor;
        helpTextBox.SelectionBackColor = cardBackColor;

        StyleLine(T("MainTitle"), 13F, FontStyle.Bold, titleColor);
        StyleLine(T("ModesTitle"), 10.5F, FontStyle.Bold, titleColor);

        StyleLine(T("Mode1Title"), 10.5F, FontStyle.Bold, titleColor);
        StyleLine(T("Mode2Title"), 10.5F, FontStyle.Bold, titleColor);

        StyleLine(T("Method1Title"), 11.5F, FontStyle.Bold, accentColor);
        StyleLine(T("Method2Title"), 11.5F, FontStyle.Bold, accentColor);
        StyleLine(T("ProblemsTitle"), 11.5F, FontStyle.Bold, accentColor);
        StyleLine(T("ContactTitle"), 11.5F, FontStyle.Bold, accentColor);

        StyleLine(T("Steps"), 10.5F, FontStyle.Bold, titleColor);

        StyleNumberedLines();
        StyleWarnings();
        StyleAllSeparators();

        helpTextBox.SelectionStart = 0;
        helpTextBox.SelectionLength = 0;
    }

    private void StyleLine(string text, float size, FontStyle style, Color color)
    {
        int start = helpTextBox.Text.IndexOf(text, StringComparison.Ordinal);

        while (start >= 0)
        {
            helpTextBox.Select(start, text.Length);
            helpTextBox.SelectionFont = new Font("Segoe UI", size, style);
            helpTextBox.SelectionColor = color;

            start = helpTextBox.Text.IndexOf(text, start + text.Length, StringComparison.Ordinal);
        }
    }

    private void StyleNumberedLines()
    {
        string[] lines = language switch
        {
            "ru" =>
            [
                "1. Убедитесь, что Download All Files отключён!!!!!!!!!!!!!!!!!!!!",
                "2. Выберите корневую папку DOOM Eternal.",
                "3. Выберите версию, до которой хотите сделать даунпатч.",
                "4. Введите логин и пароль Steam.",
                "5. Выберите папку файлов даунпатча.",
                "7. Нажмите кнопку Downpatch.",
                "8. Ждите.",
                "9. Перейдите в папку даунпатча и скопируйте все файлы в корневую папку DOOM Eternal.",
                "1. Включите Download All Files!!!!!!!!",
                "2. Выберите версию, которую хотите скачать.",
                "4. Выберите папку загрузки.",
                "5. Нажмите кнопку Downpatch.",
                "6. Ждите.",
                "8. После завершения загрузки добавьте DOOMEternalx64vk.exe в Steam как A Non-Steam Game."
            ],
            "es" =>
            [
                "1. Asegúrese de que Download All Files esté desactivado!!!!!!!!!!!!!!!!!!!!",
                "2. Seleccione la carpeta raíz de DOOM Eternal.",
                "3. Seleccione la versión a la que quiere hacer downpatch.",
                "4. Introduzca su login y contraseña de Steam.",
                "5. Seleccione la carpeta de archivos del downpatch.",
                "7. Pulse el botón Downpatch.",
                "8. Espere.",
                "9. Vaya a su carpeta de downpatch y copie todos los archivos en la carpeta raíz de DOOM Eternal.",
                "1. Active Download All Files!!!!!!!!",
                "2. Seleccione la versión que quiere descargar.",
                "4. Seleccione la carpeta de descarga.",
                "5. Pulse el botón Downpatch.",
                "6. Espere.",
                "8. Cuando termine la descarga, añada DOOMEternalx64vk.exe a Steam como Non-Steam Game."
            ],
            _ =>
            [
                "1. Make sure Download All Files is disabled!!!!!!!!!!!!!!!!!!!!",
                "2. Select DOOM Eternal root folder.",
                "3. Select the version you want to downpatch to.",
                "4. Enter your Steam login and password.",
                "5. Select downpatch files folder.",
                "7. Hit the Downpatch button.",
                "8. Wait.",
                "9. Go to your downpatch folder and copy all files over to your DOOM Eternal root folder.",
                "1. Enable Download All Files!!!!!!!!",
                "2. Select the version you want to download.",
                "4. Select Download Folder.",
                "5. Hit the Downpatch button.",
                "6. Wait.",
                "8. After download is complete, add DOOMEternalx64vk.exe as a Non-Steam Game in Steam."
            ]
        };

        foreach (string line in lines)
            StyleLine(line, 10F, FontStyle.Bold, textColor);
    }

    private void StyleWarnings()
    {
        string[] lines = language switch
        {
            "ru" =>
            [
                "- Старые версии DOOM Eternal могут иметь проблемы на новых драйверах NVIDIA, например проблемы с освещением или бочки, которые не взрываются даже после выстрела.",
                "- Подтверждённо рабочая версия драйвера: 516.94:",
                "- Если у вас видеокарта NVIDIA 40-й серии или новее, она несовместима с драйверами, которые нужны этим даунпатченным версиям DOOM Eternal для корректной работы."
            ],
            "es" =>
            [
                "- Las versiones antiguas de DOOM Eternal pueden tener problemas con drivers NVIDIA nuevos, como problemas de iluminación o barriles que no explotan aunque se les dispare.",
                "- La versión de driver 516.94 está confirmada como funcional:",
                "- Si usa una GPU NVIDIA de la serie 40 o superior, no es compatible con los drivers que esas versiones downpatcheadas de DOOM Eternal necesitan para funcionar correctamente."
            ],
            _ =>
            [
                "- Old versions of DOOM Eternal may have issues on newer NVIDIA drivers, such as lighting issues or barrels not exploding despite being shot.",
                "- Driver version 516.94 is confirmed working:",
                "- If you are running on a GPU from NVIDIA 40 series or higher, it is not compatible with the drivers that those downpatched versions of DOOM Eternal require in order to work properly."
            ]
        };

        foreach (string line in lines)
            StyleLine(line, 10F, FontStyle.Regular, darkMode ? Color.FromArgb(255, 196, 135) : Color.FromArgb(145, 80, 28));
    }

    private void StyleAllSeparators()
    {
        string separator = "------------------------------------------------------------";
        int start = helpTextBox.Text.IndexOf(separator, StringComparison.Ordinal);

        Color separatorColor = darkMode ? Color.FromArgb(82, 82, 94) : Color.FromArgb(198, 205, 216);

        while (start >= 0)
        {
            helpTextBox.Select(start, separator.Length);
            helpTextBox.SelectionFont = new Font("Consolas", 9F, FontStyle.Regular);
            helpTextBox.SelectionColor = separatorColor;

            start = helpTextBox.Text.IndexOf(separator, start + separator.Length, StringComparison.Ordinal);
        }
    }

    private sealed class RoundedPanel : Panel
    {
        private int cornerRadius = 14;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int CornerRadius
        {
            get => cornerRadius;
            set
            {
                cornerRadius = value;
                Invalidate();
            }
        }

        public RoundedPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            using GraphicsPath path = CreatePath(ClientRectangle, CornerRadius);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            using SolidBrush brush = new(BackColor);
            e.Graphics.FillPath(brush, path);

            Region = new Region(path);
        }

        private static GraphicsPath CreatePath(Rectangle rectangle, int radius)
        {
            int diameter = radius * 2;
            Rectangle arc = new(rectangle.Location, new Size(diameter, diameter));
            GraphicsPath path = new();

            path.AddArc(arc, 180, 90);

            arc.X = rectangle.Right - diameter - 1;
            path.AddArc(arc, 270, 90);

            arc.Y = rectangle.Bottom - diameter - 1;
            path.AddArc(arc, 0, 90);

            arc.X = rectangle.Left;
            path.AddArc(arc, 90, 90);

            path.CloseFigure();
            return path;
        }
    }
}
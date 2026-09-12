
using System;
using System.ComponentModel.Composition;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using Blish_HUD;
using Blish_HUD.Content;
using Blish_HUD.Controls;
using Blish_HUD.Modules;
using Blish_HUD.Modules.Managers;
using Blish_HUD.Settings;
using Blish_HUD.Graphics.UI;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Newtonsoft.Json;

namespace SociedadeDoDragao
{
    [Export(typeof(Module))]
    public class SociedadeDoDragaoModule : Module
    {
        private static readonly Logger Logger =
            Logger.GetLogger<SociedadeDoDragaoModule>();

        private const string ConfigUrl =
            "https://raw.githubusercontent.com/rikaschmitt/sociedadedodragao/main/config/config.json";

        private readonly HttpClient _httpClient =
            new HttpClient();

        private ContentsManager ContentsManager =>
            this.ModuleParameters.ContentsManager;

        private AsyncTexture2D _iconTexture;
        private AsyncTexture2D _calendarTexture;
        private AsyncTexture2D _calendarTabIcon;

        private CornerIcon _cornerIcon;
        private ContextMenuStrip _guildMenu;

        private TabbedWindow2 _calendarWindow;

        // Janela principal dos recursos da Guild.
        // _calendarWindow continua sendo usado pelo código do calendário,
        // mas agora aponta para esta janela única com abas laterais.
        private TabbedWindow2 _guildResourcesWindow;
        private Panel _guildTabSidebar;
        private Tab _guildCalendarTab;
        private Tab _guildDailyMessageTab;
        private Label _guildCalendarTabLabel;
        private Label _guildDailyMessageTabLabel;
        private Panel _guildCalendarView;
        private Panel _guildDailyMessageView;

        // Área onde o calendário é exibido e movimentado.
        private Panel _calendarViewport;

        // Coluna fixa com os dias da semana.
        private Panel _weekdayColumn;

        // Faixa semitransparente que destaca o dia atual.
        private Panel _todayHighlight;

        private Image _calendarImage;

        // Linha "Agora" que acompanha o horário atual sobre a linha do tempo.
        private Image _currentTimeLine;
        private AsyncTexture2D _currentTimeLineTexture;
        private Texture2D _currentTimeLineRawTexture;
        private Label _currentTimeLabel;
        private System.Threading.Timer _currentTimeTimer;

        private Image _calendarTitle;
        private AsyncTexture2D _calendarTitleTexture;
        private Label _calendarPeriod;
        private Label _calendarStatus;
        private Label _calendarWarning;

        // Recursos da Staff.
        // O PIN é uma barreira de acesso local ao módulo; não é uma
        // autenticação de segurança real, pois o valor fica no código.
        private const string StaffPin = "peteco";
        private bool _staffResourcesEnabled = false;
        private Panel _staffPinWindow;
        private TextBox _staffPinInput;
        private Label _staffPinStatus;
        private Label _staffPinTitle;
        private Label _staffPinSubtitle;
        private StandardButton _staffPinCloseButton;
        // Janela principal dos recursos da Staff, também organizada em abas.
        private TabbedWindow2 _staffResourcesWindow;
        private Panel _staffTabSidebar;
        private Tab _staffRaffleTab;
        private Tab _staffQuickMessagesTab;
        private Panel _staffRaffleContent;
        private Panel _staffQuickMessagesContent;
        private Label _staffRaffleTabLabel;
        private Label _staffQuickMessagesTabLabel;
        private Panel _staffRaffleView;
        private Panel _staffQuickMessagesView;

        // Mensagens rápidas.
        private StandardWindow _quickMessagesWindow;
        private Label _quickMessageRecruitmentLabel;
        private Label _quickMessageGuildMissionLabel;
        private Label _quickMessageResetLabel;
        private string _quickMessageRecruitment = string.Empty;
        private string _quickMessageGuildMission = string.Empty;
        private string _quickMessageReset = string.Empty;

        // Mensagem do Dia.
        private StandardWindow _dailyMessageWindow;
        private Image _dailyMessageImage;
        private AsyncTexture2D _dailyMessageTexture;
        private Texture2D _dailyMessageRawTexture;
        private readonly Random _dailyMessageRandom = new Random();

        private SettingEntry<string> _dailyMessageDateSetting;
        private SettingEntry<int> _dailyMessageImageSetting;

        // Sorteador de Participantes.
        private StandardWindow _raffleWindow;
        private readonly TextBox[] _raffleInputs = new TextBox[18];
        private readonly bool[] _raffleWinnerDisabled = new bool[18];
        private readonly Panel[] _raffleDisabledOverlays = new Panel[18];
        private readonly Label[] _raffleDisabledLabels = new Label[18];
        private Label _raffleResultLabel;
        private Label _raffleWinnerLabel;
        private StandardButton _raffleDrawButton;
        private StandardButton _raffleResetButton;
        private readonly Random _raffleRandom = new Random();

        // Dimensões/origem da imagem.
        private int _calendarOriginalWidth;
        private int _calendarOriginalHeight;

        // Escala usada para fazer a imagem caber verticalmente.
        private float _calendarScale = 1.0f;

        // Arraste horizontal.
        private bool _isDragging;
        private Point _lastMousePosition = Point.Zero;

        // Layout.
        // Layout da janela no estilo das telas nativas do Blish HUD.
        // A coluna lateral fica dentro do ContentRegion, nunca sobre a moldura.
        private const int GuildTabSidebarWidth = 190;
        private const int CalendarContentLeft = 20;

        // Padding uniforme do conteúdo da janela.
        private const int ContentPadding = 20;

        private const int ViewportLeft = ContentPadding;
        private const int ViewportTop = 80;
        private const int ViewportRight = ContentPadding;
        private const int ViewportBottom = ContentPadding;

        private const int WeekdayColumnLeft = ContentPadding;
        private const int WeekdayColumnWidth = 95;

        // Medidas da nova arte do calendário (2048 x 676).
        // Os cabeçalhos dos dias foram medidos diretamente na imagem.
        // Na nova arte:
        // - o início do texto "14:00" está em X ≈ 16 px;
        // - a distância entre o início de "14:00" e o início de "14:30"
        //   é ≈ 156 px.
        // Portanto, usamos 312 px por hora.
        private const float CalendarTimelineStartX = 16.0f;
        private const float CalendarTimelineStartMinutes = 14.0f * 60.0f;
        private const float CalendarTimelinePixelsPerHour = 312.0f;

        // Centro vertical dos cabeçalhos dos dias na arte original.
        // Usamos posições individuais para evitar o acúmulo de erro
        // que acontecia ao repetir um passo fixo entre os dias.
        private const float CalendarDayTopMargin = 56.0f;
        private const float CalendarEventCardHeight = 120.0f;
        private const float CalendarEventCardGap = 16.0f;

        // Altura lógica da arte descrita acima: 56 + 7*120 + 6*16 + 34.
        // A imagem fornecida está exportada em uma escala proporcional.
        private const float CalendarLogicalImageHeight =
            CalendarDayTopMargin +
            (7.0f * CalendarEventCardHeight) +
            (6.0f * CalendarEventCardGap) +
            34.0f;

        private const int CalendarHeaderHeight = 48;

        // A linha "Agora" começa centralizada na janela quando o calendário
        // é carregado, permitindo identificar imediatamente o próximo evento.
        private bool _calendarUserHasDragged = false;

        private static readonly string[] Weekdays =
        {
            "Segunda",
            "Terça",
            "Quarta",
            "Quinta",
            "Sexta",
            "Sábado",
            "Domingo"
        };

        private class RemoteConfig
        {
            public string versao { get; set; }

            public CalendarConfig calendario { get; set; }

            public RaffleConfig sorteio { get; set; }

            public QuickMessagesConfig mensagens { get; set; }

            public Dictionary<string, string> msgDia { get; set; }
        }

        private class QuickMessagesConfig
        {
            public string msgRecrutamento { get; set; }

            public string msgGuildMission { get; set; }

            public string msgReset { get; set; }
        }

        private class RaffleConfig
        {
            public string mensagemSorteado { get; set; }
        }

        private class CalendarConfig
        {
            public string imagem { get; set; }

            public string periodoCalendario { get; set; }

            public string ultimaAtualizacao { get; set; }
        }

        [ImportingConstructor]
        public SociedadeDoDragaoModule(
            [Import("ModuleParameters")] ModuleParameters moduleParameters)
            : base(moduleParameters)
        {
        }

        protected override void DefineSettings(
            SettingCollection settings)
        {
            // Guarda localmente qual imagem foi escolhida para o dia.
            // Assim, todos os cliques no mesmo dia mostram a mesma mensagem,
            // mesmo depois de fechar e reabrir o Blish HUD.
            _dailyMessageDateSetting =
                settings.DefineSetting(
                    "MensagemDoDia_Data",
                    string.Empty
                );

            _dailyMessageImageSetting =
                settings.DefineSetting(
                    "MensagemDoDia_Imagem",
                    0
                );
        }

        protected override async Task LoadAsync()
        {
            Logger.Info(
                "Sociedade do Dragão carregando..."
            );

            // ========================================================
            // ÍCONE
            // ========================================================

            _iconTexture =
                ContentsManager.GetTexture(
                    "icon.png",
                    null
                );

            _calendarTabIcon =
                ContentsManager.GetTexture(
                    "icon-calendar.png",
                    null
                );

            _cornerIcon = new CornerIcon
            {
                Icon = _iconTexture,
                BasicTooltipText = "Sociedade do Dragão [BR]",
                Priority = 1645843523,
                Parent = GameService.Graphics.SpriteScreen
            };

            // ========================================================
            // MENU DROPDOWN
            // ========================================================

            _guildMenu =
                new ContextMenuStrip();

            var guildResourcesMenuItem =
                _guildMenu.AddMenuItem(
                    "Recursos da Guild"
                );

            guildResourcesMenuItem.Click += (sender, e) =>
            {
                ShowGuildResourcesWindow();
            };

            var staffResourcesMenuItem =
                _guildMenu.AddMenuItem(
                    "Recursos da Staff"
                );

            staffResourcesMenuItem.Click += (sender, e) =>
            {
                if (!_staffResourcesEnabled)
                {
                    ShowStaffPinWindow();
                }
                else
                {
                    ShowStaffResourcesWindow();
                }
            };

            // ========================================================
            // TAMANHO INICIAL DA JANELA
            // ========================================================

            int screenWidth =
                GameService.Graphics.SpriteScreen.Width;

            int screenHeight =
                GameService.Graphics.SpriteScreen.Height;

            int initialWidth =
                Math.Max(
                    900,
                    Math.Min(
                        1300,
                        (int)(screenWidth * 0.96f)
                    )
                );

            int initialHeight =
                Math.Max(
                    900,
                    Math.Min(
                        1100,
                        (int)(screenHeight * 0.98f)
                    )
                );

            // ========================================================
            // JANELA
            // ========================================================

            var windowBackground =
                AsyncTexture2D.FromAssetId(155985);

            _calendarWindow =
                new TabbedWindow2(
                    windowBackground,
                    new Rectangle(
                        40,
                        26,
                        913,
                        691
                    ),
                    new Rectangle(
                        90,
                        30,
                        819,
                        660
                    ),
                    new Point(
                        initialWidth,
                        initialHeight
                    ))
                {
                    Parent =
                        GameService.Graphics.SpriteScreen,

                    Title =
                        "Recursos da Guild",

                    Subtitle =
                        "Sociedade do Dragão [BR]",

                    Emblem =
                        _iconTexture,

                    Location =
                        new Point(
                            Math.Max(
                                0,
                                (screenWidth - initialWidth) / 2
                            ),
                            Math.Max(
                                0,
                                (screenHeight - initialHeight) / 2
                            )
                        ),

                    CanResize =
                        true,

                    SavesPosition =
                        true,

                    Id =
                        "SociedadeDoDragao_RecursosGuild"
                };

            _guildResourcesWindow = _calendarWindow;

            // ========================================================
            // ABAS LATERAIS
            // ========================================================

            CreateGuildTabs();

            // ========================================================
            // CABEÇALHO
            // ========================================================

            _calendarTitleTexture =
                ContentsManager.GetTexture(
                    "titleCalendar.png",
                    null
                );

            _calendarTitle =
                new Image
                {
                    Texture =
                        _calendarTitleTexture,

                    Location =
                        new Point(
                            20,
                            0
                        ),

                    Size =
                        new Point(
                            348,
                            36
                        ),

                    Parent =
                        _guildCalendarView,

                    ZIndex =
                        20
                };

            _calendarPeriod =
                new Label
                {
                    Text =
                        "Período: carregando...",

                    AutoSizeWidth =
                        true,

                    AutoSizeHeight =
                        true,

                    Parent =
                        _guildCalendarView,

                    Font =
                        GameService.Content.DefaultFont16,

                    TextColor =
                        Color.White,

                    StrokeText =
                        false,

                    ZIndex =
                        20
                };

            _calendarStatus =
                new Label
                {
                    Text =
                        "Última atualização: carregando...",

                    AutoSizeWidth =
                        true,

                    AutoSizeHeight =
                        true,

                    Parent =
                        _guildCalendarView,

                    Font =
                        GameService.Content.DefaultFont14,

                    TextColor =
                        Color.LightGray,

                    StrokeText =
                        false,

                    ZIndex =
                        20
                };

            _calendarWarning =
                new Label
                {
                    Text =
                        "",

                    Location =
                        new Point(
                            20,
                            34
                        ),

                    AutoSizeWidth =
                        false,

                    Width =
                        420,

                    AutoSizeHeight =
                        true,

                    Parent =
                        _guildCalendarView,

                    Font =
                        GameService.Content.DefaultFont14,

                    TextColor =
                        Color.LightGray,

                    StrokeText =
                        false,

                    ZIndex =
                        20
                };

            PositionCalendarHeader();

            // ========================================================
            // VIEWPORT
            // ========================================================

            _calendarViewport =
                new Panel
                {
                    Location =
                        new Point(
                            ViewportLeft,
                            ViewportTop
                        ),

                    Size =
                        new Point(
                            800,
                            500
                        ),

                    Parent =
                        _guildCalendarView,

                    ShowBorder =
                        false,

                    ClipsBounds =
                        true,

                    ZIndex =
                        1
                };

            // ========================================================
            // DESTAQUE DO DIA ATUAL
            // ========================================================

            _todayHighlight =
                new Panel
                {
                    Location =
                        Point.Zero,

                    Size =
                        new Point(
                            1,
                            1
                        ),

                    Parent =
                        _guildCalendarView,

                    BackgroundColor =
                        Color.FromNonPremultiplied(
                            255,
                            225,
                            0,
                            25
                        ),

                    ShowBorder =
                        false,

                    ZIndex =
                        0
                };

            // ========================================================
            // COLUNA FIXA DOS DIAS
            // ========================================================

            _weekdayColumn =
                new Panel
                {
                    // A coluna é um overlay FIXO, irmão do viewport.
                    // Assim ela fica acima de toda a árvore do calendário
                    // e nunca pode ser coberta pela imagem.
                    Location =
                        new Point(
                            ViewportLeft,
                            ViewportTop
                        ),

                    Size =
                        new Point(
                            WeekdayColumnWidth,
                            500
                        ),

                    // Importante: o overlay fica como filho direto da view,
                    // no mesmo nível do viewport. Isso garante que o ZIndex
                    // possa colocá-lo acima de toda a imagem do calendário.
                    Parent =
                        _guildCalendarView,

                    ShowBorder =
                        false,

                    ClipsBounds =
                        true,

                    ZIndex =
                        300
                };

            CreateWeekdayLabels();
            UpdateTodayWeekdayHighlight();

            // ========================================================
            // IMAGEM
            // ========================================================

            _calendarImage =
                new Image
                {
                    Location =
                        Point.Zero,

                    Size =
                        new Point(
                            800,
                            500
                        ),

                    Parent =
                        _calendarViewport,

                    ZIndex =
                        1
                };

            // ========================================================
            // LINHA "AGORA"
            // ========================================================

            _currentTimeLine =
                new Image
                {
                    Location =
                        Point.Zero,

                    Size =
                        new Point(
                            2,
                            100
                        ),

                    Parent =
                        _calendarViewport,

                    Tint =
                        new Color(
                            255,
                            225,
                            0
                        ),

                    ZIndex =
                        10
                };

            _currentTimeLabel =
                new Label
                {
                    Text =
                        "Agora (Brasil)",

                    Parent =
                        _guildCalendarView,

                    Font =
                        GameService.Content.DefaultFont16,

                    TextColor =
                        new Color(
                            255,
                            225,
                            0
                        ),

                    StrokeText =
                        false,

                    AutoSizeWidth =
                        true,

                    AutoSizeHeight =
                        true,

                    ZIndex =
                        11
                };

            CreateCurrentTimeLineTexture();

            // Atualiza o indicador uma vez por segundo. A linha só
            // muda de posição quando o horário atual muda.
            _currentTimeTimer =
                new System.Threading.Timer(
                    state =>
                    {
                        GameService.Graphics.QueueMainThreadRender(
                            graphicsDevice =>
                            {
                                UpdateCurrentTimeLine();
                            }
                        );
                    },
                    null,
                    TimeSpan.Zero,
                    TimeSpan.FromSeconds(1)
                );

            // ========================================================
            // ARRASTAR SOMENTE HORIZONTALMENTE
            // ========================================================

            _calendarImage.LeftMouseButtonPressed +=
                (sender, e) =>
                {
                    _isDragging = true;
                    _calendarUserHasDragged = true;

                    _lastMousePosition =
                        GameService.Input.Mouse.Position;
                };

            _calendarImage.MouseMoved +=
                (sender, e) =>
                {
                    if (!_isDragging)
                    {
                        return;
                    }

                    Point currentMousePosition =
                        GameService.Input.Mouse.Position;

                    int deltaX =
                        currentMousePosition.X -
                        _lastMousePosition.X;

                    if (deltaX == 0)
                    {
                        return;
                    }

                    _calendarImage.Location =
                        new Point(
                            _calendarImage.Location.X +
                            deltaX,
                            0
                        );

                    _lastMousePosition =
                        currentMousePosition;

                    ConstrainCalendarImage();
                    UpdateCurrentTimeLine();
                };

            _calendarImage.LeftMouseButtonReleased +=
                (sender, e) =>
                {
                    _isDragging = false;
                };

            _calendarImage.MouseLeft +=
                (sender, e) =>
                {
                    _isDragging = false;
                };

            // ========================================================
            // MENSAGEM DO DIA
            // ========================================================

            _dailyMessageImage =
                new Image
                {
                    Location =
                        new Point(
                            CalendarContentLeft,
                            15
                        ),

                    Size =
                        new Point(
                            600,
                            600
                        ),

                    Parent =
                        _guildDailyMessageView,

                    Visible =
                        false,

                    ZIndex =
                        10
                };


            // ========================================================
            // RESPONSIVIDADE
            // ========================================================

            _calendarWindow.Resized +=
                (sender, e) =>
                {
                    ResizeCalendarViewport();

                    PositionCalendarHeader();

                    ResizeCalendarImage();

                    ResizeWeekdayColumn();
                    UpdateTodayWeekdayHighlight();
                    UpdateTodayHighlight();
                    FitDailyMessageImage();

                    if (!_calendarUserHasDragged)
                    {
                        CenterCalendarOnCurrentTime();
                    }

                    UpdateCurrentTimeLine();
                };

            ResizeCalendarViewport();

            PositionCalendarHeader();

            ResizeCalendarImage();

            ResizeWeekdayColumn();

            CenterCalendarOnCurrentTime();

            UpdateCurrentTimeLine();

            // ========================================================
            // CLIQUE NO ÍCONE
            // ========================================================

            _cornerIcon.Click +=
                (sender, e) =>
                {
                    _guildMenu.Show(
                        _cornerIcon
                    );
                };

            await Task.CompletedTask;

            Logger.Info(
                "Sociedade do Dragão carregado com sucesso!"
            );
        }

        // ============================================================
        // RECURSOS DA GUILD / ABAS LATERAIS
        // ============================================================

        private void ShowGuildResourcesWindow()
        {
            if (_guildResourcesWindow == null)
            {
                return;
            }

            _calendarUserHasDragged = false;
            _guildResourcesWindow.Show();
            SetGuildTab(0);

            if (
                _calendarOriginalWidth > 0 &&
                _calendarOriginalHeight > 0
            )
            {
                CenterCalendarOnCurrentTime();
            }

            _ = LoadCalendarAsync();
        }

        private void CreateGuildTabs()
        {
            if (_calendarWindow == null)
            {
                return;
            }

            _guildCalendarView = new Panel
            {
                Size = _calendarWindow.ContentRegion.Size,
                ShowBorder = false,
                ClipsBounds = true
            };

            _guildDailyMessageView = new Panel
            {
                Size = _calendarWindow.ContentRegion.Size,
                ShowBorder = false,
                ClipsBounds = true
            };

            _guildCalendarTab =
                new Tab(
                    _calendarTabIcon,
                    () => new PanelView(_guildCalendarView),
                    "Calendário"
                );

            _guildDailyMessageTab =
                new Tab(
                    _iconTexture,
                    () => new PanelView(_guildDailyMessageView),
                    "Mensagem do Dia"
                );

            _calendarWindow.Tabs.Add(_guildCalendarTab);
            _calendarWindow.Tabs.Add(_guildDailyMessageTab);

            _calendarWindow.TabChanged +=
                (sender, e) =>
                {
                    if (e.NewValue == _guildCalendarTab)
                    {
                        _calendarUserHasDragged = false;

                        if (
                            _calendarOriginalWidth > 0 &&
                            _calendarOriginalHeight > 0
                        )
                        {
                            CenterCalendarOnCurrentTime();
                        }

                        _ = LoadCalendarAsync();
                    }
                    else if (e.NewValue == _guildDailyMessageTab)
                    {
                        _ = LoadDailyMessageAsync();
                    }
                };

            _calendarWindow.Resized +=
                (sender, e) =>
                {
                    ResizeTabViewPanels();
                };

            _calendarWindow.SelectedTab = _guildCalendarTab;
        }

        private void ResizeTabViewPanels()
        {
            if (_calendarWindow != null)
            {
                Point size = _calendarWindow.ContentRegion.Size;

                if (_guildCalendarView != null)
                    _guildCalendarView.Size = size;

                if (_guildDailyMessageView != null)
                    _guildDailyMessageView.Size = size;
            }

            if (_staffResourcesWindow != null)
            {
                Point size = _staffResourcesWindow.ContentRegion.Size;

                if (_staffRaffleContent != null)
                    _staffRaffleContent.Size = size;

                if (_staffQuickMessagesContent != null)
                    _staffQuickMessagesContent.Size = size;
            }
        }

        private void SetGuildTab(int tabIndex)
        {
            if (_calendarWindow == null)
                return;

            _calendarWindow.SelectedTab =
                tabIndex == 0
                    ? _guildCalendarTab
                    : _guildDailyMessageTab;
        }

        // ============================================================
        // CRIAR COLUNA DOS DIAS
        // ============================================================

        private void CreateWeekdayLabels()
        {
            if (_weekdayColumn == null)
            {
                return;
            }

            _weekdayColumn.ClearChildren();

            foreach (string weekday in Weekdays)
            {
                var label =
                    new Label
                    {
                        Text =
                            weekday,

                        Font =
                            GameService.Content.DefaultFont16,

                        TextColor =
                            weekday == GetTodayWeekday()
                                ? new Color(255, 225, 0)
                                : Color.White,

                        AutoSizeWidth =
                            true,

                        AutoSizeHeight =
                            true,

                        Parent =
                            _weekdayColumn,

                        StrokeText =
                            false,

                        ZIndex =
                            20
                    };

                PositionWeekdayLabel(
                    label,
                    0
                );
            }

            PositionWeekdayLabels();
        }

        private string GetTodayWeekday()
        {
            int dayIndex =
                ((int)DateTime.Now.DayOfWeek + 6) % 7;

            return Weekdays[dayIndex];
        }

        private void UpdateTodayWeekdayHighlight()
        {
            if (_weekdayColumn == null)
            {
                return;
            }

            string today = GetTodayWeekday();

            foreach (var child in _weekdayColumn.Children)
            {
                if (child is Label label)
                {
                    string weekday = label.Text.TrimStart('>', ' ');

                    if (weekday == today)
                    {
                        label.Text = $"> {weekday}";
                        label.TextColor = new Color(255, 225, 0);
                    }
                    else
                    {
                        label.Text = weekday;
                        label.TextColor = Color.White;
                    }
                }
            }

            PositionWeekdayLabels();
        }

        private void PositionCalendarHeader()
        {
            if (
                _calendarWindow == null ||
                _calendarTitle == null ||
                _calendarPeriod == null ||
                _calendarStatus == null ||
                _calendarWarning == null
            )
            {
                return;
            }

            const int leftMargin = ContentPadding;
            const int rightMargin = ContentPadding;

            _calendarTitle.Location =
                new Point(
                    leftMargin,
                    2
                );


            _calendarWarning.Location =
                new Point(
                    leftMargin,
                    28
                );

            int rightWidth =
                Math.Max(
                    _calendarPeriod.Width,
                    _calendarStatus.Width
                );

            int rightX =
                Math.Max(
                    leftMargin,
                    _calendarWindow.ContentRegion.Width -
                    rightWidth -
                    rightMargin
                );

            _calendarPeriod.Location =
                new Point(
                    rightX,
                    2
                );

            _calendarStatus.Location =
                new Point(
                    rightX,
                    28
                );
        }


        private void PositionWeekdayLabels()
        {
            if (_weekdayColumn == null)
            {
                return;
            }

            int index = 0;

            foreach (var child in _weekdayColumn.Children)
            {
                if (child is Label label)
                {
                    PositionWeekdayLabel(label, index);
                    index++;
                }
            }
        }

        private void PositionWeekdayLabel(
            Label label,
            int index)
        {
            if (
                _weekdayColumn == null ||
                label == null ||
                index < 0 ||
                index >= Weekdays.Length
            )
            {
                return;
            }

            float logicalCenterY =
                CalendarDayTopMargin +
                (CalendarEventCardHeight / 2.0f) +
                index *
                (CalendarEventCardHeight + CalendarEventCardGap);

            // Converte as medidas lógicas para a escala física da imagem.
            float imageCoordinateScale =
                _calendarOriginalHeight /
                CalendarLogicalImageHeight;

            float scaledCenterY =
                logicalCenterY *
                imageCoordinateScale *
                _calendarScale;

            // A coluna fica fixa no topo do viewport; portanto o deslocamento
            // vertical da imagem precisa ser aplicado diretamente ao label.
            int imageTop =
                _calendarImage != null
                    ? _calendarImage.Location.Y
                    : 0;

            int y =
                (int)(
                    imageTop +
                    scaledCenterY -
                    label.Height / 2.0f
                );

            int rightPadding = 10;

            int x =
                Math.Max(
                    0,
                    WeekdayColumnWidth -
                    label.Width -
                    rightPadding
                );

            label.Location =
                new Point(
                    x,
                    y
                );
        }

        // ============================================================
        // DESTAQUE DO DIA ATUAL
        // ============================================================

        private void UpdateTodayHighlight()
        {
            if (
                _calendarWindow == null ||
                _todayHighlight == null ||
                _calendarImage == null ||
                _calendarOriginalHeight <= 0
            )
            {
                return;
            }

            float imageCoordinateScale =
                _calendarOriginalHeight /
                CalendarLogicalImageHeight;

            int todayIndex =
                ((int)DateTime.Now.DayOfWeek + 6) % 7;

            float logicalTop =
                CalendarDayTopMargin +
                todayIndex *
                (
                    CalendarEventCardHeight +
                    CalendarEventCardGap
                );

            float scaledTop =
                logicalTop *
                imageCoordinateScale *
                _calendarScale;

            float scaledHeight =
                CalendarEventCardHeight *
                imageCoordinateScale *
                _calendarScale;

            int y =
                ViewportTop +
                _calendarImage.Location.Y +
                (int)scaledTop;

            int height =
                Math.Max(
                    1,
                    (int)scaledHeight
                );

            int width =
                Math.Max(
                    1,
                    _calendarWindow.ContentRegion.Width -
                    ContentPadding -
                    ContentPadding
                );

            _todayHighlight.Location =
                new Point(
                    ContentPadding,
                    y
                );

            _todayHighlight.Size =
                new Point(
                    width,
                    height
                );
        }

        // ============================================================
        // VIEWPORT
        // ============================================================

        private void ResizeCalendarViewport()
        {
            if (
                _calendarWindow == null ||
                _calendarViewport == null
            )
            {
                return;
            }

            int viewportWidth =
                _calendarWindow.ContentRegion.Width -
                ViewportLeft -
                ViewportRight;

            int viewportHeight =
                _calendarWindow.ContentRegion.Height -
                ViewportTop -
                ViewportBottom;

            if (
                viewportWidth <= 0 ||
                viewportHeight <= 0
            )
            {
                return;
            }

            _calendarViewport.Location =
                new Point(
                    ViewportLeft,
                    ViewportTop
                );

            _calendarViewport.Size =
                new Point(
                    viewportWidth,
                    viewportHeight
                );
        }

        // ============================================================
        // DIMENSIONAR IMAGEM PROPORCIONALMENTE
        // ============================================================

        private void ResizeCalendarImage()
        {
            if (
                _calendarViewport == null ||
                _calendarImage == null ||
                _calendarOriginalWidth <= 0 ||
                _calendarOriginalHeight <= 0
            )
            {
                return;
            }

            int viewportWidth =
                _calendarViewport.Width;

            int viewportHeight =
                _calendarViewport.Height;

            if (
                viewportWidth <= 0 ||
                viewportHeight <= 0
            )
            {
                return;
            }

            // A prioridade é fazer toda a altura da arte caber.
            // Nunca aumentamos a imagem além do tamanho original.
            _calendarScale =
                Math.Min(
                    1.0f,
                    (float)viewportHeight /
                    _calendarOriginalHeight
                );

            int displayWidth =
                Math.Max(
                    1,
                    (int)(
                        _calendarOriginalWidth *
                        _calendarScale
                    )
                );

            int displayHeight =
                Math.Max(
                    1,
                    (int)(
                        _calendarOriginalHeight *
                        _calendarScale
                    )
                );

            _calendarImage.Size =
                new Point(
                    displayWidth,
                    displayHeight
                );

            // Centraliza verticalmente.
            int imageY =
                Math.Max(
                    0,
                    (viewportHeight -
                     displayHeight) / 2
                );

            _calendarImage.Location =
                new Point(
                    _calendarImage.Location.X,
                    imageY
                );

            ConstrainCalendarImage();

            UpdateWeekdayColumnPosition();
            UpdateTodayHighlight();

            UpdateCurrentTimeLine();
        }

        // ============================================================
        // LINHA "AGORA"
        // ============================================================

        private void CreateCurrentTimeLineTexture()
        {
            GameService.Graphics.QueueMainThreadRender(
                graphicsDevice =>
                {
                    try
                    {
                        _currentTimeLineRawTexture =
                            new Texture2D(
                                graphicsDevice,
                                1,
                                1
                            );

                        _currentTimeLineRawTexture.SetData(
                            new[]
                            {
                                Color.White
                            }
                        );

                        _currentTimeLineTexture =
                            new AsyncTexture2D(
                                _currentTimeLineRawTexture
                            );

                        if (_currentTimeLine != null)
                        {
                            _currentTimeLine.Texture =
                                _currentTimeLineTexture;
                        }

                        UpdateCurrentTimeLine();
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(
                            $"Erro ao criar textura da linha de horário: {ex.Message}"
                        );
                    }
                }
            );
        }

        private float GetCurrentTimePixel()
        {
            // O calendário usa o horário oficial de Brasília,
            // independentemente do fuso horário do computador do jogador.
            DateTime now = GetBrazilTime();

            float currentMinutes =
                now.Hour * 60.0f +
                now.Minute +
                now.Second / 60.0f;

            return
                CalendarTimelineStartX +
                (
                    (
                        currentMinutes -
                        CalendarTimelineStartMinutes
                    ) /
                    60.0f
                ) *
                CalendarTimelinePixelsPerHour;
        }

        private static DateTime GetBrazilTime()
        {
            try
            {
                // Windows utiliza este identificador para o fuso
                // de Brasília.
                TimeZoneInfo brazilTimeZone =
                    TimeZoneInfo.FindSystemTimeZoneById(
                        "E. South America Standard Time"
                    );

                return TimeZoneInfo.ConvertTimeFromUtc(
                    DateTime.UtcNow,
                    brazilTimeZone
                );
            }
            catch (TimeZoneNotFoundException)
            {
                // Fallback para ambientes onde o identificador acima
                // não estiver disponível.
                return DateTime.UtcNow.AddHours(-3);
            }
            catch (InvalidTimeZoneException)
            {
                return DateTime.UtcNow.AddHours(-3);
            }
        }

        private void CenterCalendarOnCurrentTime()
        {
            if (
                _calendarViewport == null ||
                _calendarImage == null ||
                _calendarOriginalWidth <= 0
            )
            {
                return;
            }

            float currentTimePixel =
                GetCurrentTimePixel();

            float scaledCurrentTimePixel =
                currentTimePixel *
                _calendarScale;

            int desiredX =
                (
                    _calendarViewport.Width / 2
                ) -
                (int)scaledCurrentTimePixel;

            _calendarImage.Location =
                new Point(
                    desiredX,
                    _calendarImage.Location.Y
                );

            ConstrainCalendarImage();

            UpdateTodayHighlight();
            UpdateCurrentTimeLine();
        }

        private void UpdateCurrentTimeLine()
        {
            if (
                _calendarViewport == null ||
                _calendarImage == null ||
                _currentTimeLine == null ||
                _currentTimeLabel == null ||
                _calendarOriginalWidth <= 0
            )
            {
                return;
            }

            float currentTimePixel =
                GetCurrentTimePixel();

            int lineX =
                _calendarImage.Location.X +
                (int)(
                    currentTimePixel *
                    _calendarScale
                );

            int lineY =
                _calendarImage.Location.Y;

            int lineHeight =
                _calendarImage.Height;

            _currentTimeLine.Location =
                new Point(
                    lineX,
                    lineY
                );

            _currentTimeLine.Size =
                new Point(
                    2,
                    Math.Max(
                        1,
                        lineHeight
                    )
                );

            // O texto fica acima da régua de horários, sem sobrepor
            // os horários da imagem.
            _currentTimeLabel.Location =
                new Point(
                    _calendarViewport.Location.X +
                    lineX -
                    _currentTimeLabel.Width / 2,
                    Math.Max(
                        0,
                        _calendarViewport.Location.Y +
                        lineY -
                        _currentTimeLabel.Height -
                        6
                    )
                );
        }

        // ============================================================
        // COLUNA DOS DIAS
        // ============================================================

        private void ResizeWeekdayColumn()
        {
            if (
                _calendarWindow == null ||
                _weekdayColumn == null
            )
            {
                return;
            }

            int height =
                _calendarViewport != null
                    ? _calendarViewport.Height
                    : _calendarWindow.ContentRegion.Height -
                      ViewportTop -
                      ViewportBottom;

            if (height <= 0)
            {
                return;
            }

            // O painel é um overlay irmão do viewport.
            // Ele permanece exatamente sobre a faixa esquerda do viewport.
            _weekdayColumn.Location =
                new Point(
                    ViewportLeft,
                    ViewportTop
                );

            _weekdayColumn.Size =
                new Point(
                    WeekdayColumnWidth,
                    height
                );

            PositionWeekdayLabels();
        }

        private void UpdateWeekdayColumnPosition()
        {
            if (
                _weekdayColumn == null ||
                _calendarImage == null
            )
            {
                return;
            }

            // A coluna é um overlay FIXO sobre o viewport.
            // Ela nunca recebe o X/Y da imagem e nunca participa do arraste.
            // Somente os labels usam imageTop para acompanhar as linhas dos dias.
            _weekdayColumn.Location =
                new Point(
                    ViewportLeft,
                    ViewportTop
                );

            PositionWeekdayLabels();
        }

        // ============================================================
        // LIMITAR MOVIMENTO HORIZONTAL
        // ============================================================

        private void ConstrainCalendarImage()
        {
            if (
                _calendarViewport == null ||
                _calendarImage == null
            )
            {
                return;
            }

            int viewportWidth =
                _calendarViewport.Width;

            if (viewportWidth <= 0)
            {
                return;
            }

            int newX =
                _calendarImage.Location.X;

            if (
                _calendarImage.Width <=
                viewportWidth
            )
            {
                // Se couber inteira, centraliza horizontalmente.
                newX =
                    (
                        viewportWidth -
                        _calendarImage.Width
                    ) / 2;
            }
            else
            {
                int minimumX =
                    viewportWidth -
                    _calendarImage.Width;

                int maximumX =
                    0;

                newX =
                    Math.Max(
                        minimumX,
                        Math.Min(
                            maximumX,
                            newX
                        )
                    );
            }

            _calendarImage.Location =
                new Point(
                    newX,
                    _calendarImage.Location.Y
                );
        }

        // ============================================================
        // CARREGAR CALENDÁRIO
        // ============================================================

        private async Task LoadCalendarAsync()
        {
            const int maxAttempts = 3;

            _calendarPeriod.Text =
                "Período: carregando...";

            _calendarStatus.Text =
                "Última atualização: carregando...";

            Logger.Info(
                "Baixando configuração remota..."
            );

            // ----------------------------------------------------
            // CONFIG.JSON
            // ----------------------------------------------------

            string json = null;
            Exception lastConfigException = null;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    Logger.Info(
                        $"Tentativa {attempt}/{maxAttempts} para carregar o config.json..."
                    );

                    json =
                        await _httpClient.GetStringAsync(
                            ConfigUrl
                        );

                    break;
                }
                catch (Exception ex)
                {
                    lastConfigException = ex;

                    Logger.Warn(
                        $"Falha ao carregar config.json na tentativa {attempt}/{maxAttempts}: {ex.Message}"
                    );

                    if (attempt < maxAttempts)
                    {
                        await Task.Delay(
                            TimeSpan.FromSeconds(attempt)
                        );
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                Logger.Warn(
                    $"Não foi possível carregar o config.json após {maxAttempts} tentativas: {lastConfigException?.Message}"
                );

                _calendarPeriod.Text =
                    "Período: não disponível";

                _calendarStatus.Text =
                    "Última atualização: não foi possível carregar o calendário.";

                return;
            }

            try
            {
                var config =
                    JsonConvert.DeserializeObject<RemoteConfig>(
                        json
                    );

                if (
                    config == null ||
                    config.calendario == null ||
                    string.IsNullOrWhiteSpace(
                        config.calendario.imagem
                    )
                )
                {
                    throw new Exception(
                        "Configuração do calendário inválida."
                    );
                }

                string periodo =
                    config.calendario.periodoCalendario;

                string ultimaAtualizacao =
                    config.calendario.ultimaAtualizacao;

                if (string.IsNullOrWhiteSpace(periodo))
                {
                    periodo =
                        "não informado";
                }

                if (
                    string.IsNullOrWhiteSpace(
                        ultimaAtualizacao
                    )
                )
                {
                    ultimaAtualizacao =
                        "não informada";
                }

                _calendarPeriod.Text =
                    $"Período: {periodo}";

                _calendarStatus.Text =
                    $"Última atualização: {ultimaAtualizacao}";

                Logger.Info(
                    $"Calendário encontrado: {config.calendario.imagem}"
                );

                // ----------------------------------------------------
                // IMAGEM
                // ----------------------------------------------------

                byte[] imageBytes = null;
                Exception lastImageException = null;

                for (int attempt = 1; attempt <= maxAttempts; attempt++)
                {
                    try
                    {
                        Logger.Info(
                            $"Tentativa {attempt}/{maxAttempts} para carregar a imagem do calendário..."
                        );

                        imageBytes =
                            await _httpClient.GetByteArrayAsync(
                                config.calendario.imagem
                            );

                        break;
                    }
                    catch (Exception ex)
                    {
                        lastImageException = ex;

                        Logger.Warn(
                            $"Falha ao carregar imagem do calendário na tentativa {attempt}/{maxAttempts}: {ex.Message}"
                        );

                        if (attempt < maxAttempts)
                        {
                            await Task.Delay(
                                TimeSpan.FromSeconds(attempt)
                            );
                        }
                    }
                }

                if (imageBytes == null || imageBytes.Length == 0)
                {
                    Logger.Warn(
                        $"Não foi possível carregar a imagem após {maxAttempts} tentativas: {lastImageException?.Message}"
                    );

                    _calendarStatus.Text =
                        "Última atualização: não foi possível carregar a imagem.";

                    return;
                }

                var imageStream =
                    new System.IO.MemoryStream(
                        imageBytes
                    );

                GameService.Graphics.QueueMainThreadRender(
                    graphicsDevice =>
                    {
                        try
                        {
                            var texture =
                                Texture2D.FromStream(
                                    graphicsDevice,
                                    imageStream
                                );

                            _calendarTexture =
                                new AsyncTexture2D(
                                    texture
                                );

                            _calendarOriginalWidth =
                                texture.Width;

                            _calendarOriginalHeight =
                                texture.Height;

                            _calendarImage.Texture =
                                _calendarTexture;

                            ResizeCalendarImage();

                            // A escala da imagem só é conhecida depois que
                            // a textura foi carregada. Por isso, o
                            // posicionamento definitivo do "Agora" precisa
                            // acontecer aqui, depois do ResizeCalendarImage().
                            if (!_calendarUserHasDragged)
                            {
                                CenterCalendarOnCurrentTime();
                            }

                            Logger.Info(
                                $"Calendário carregado: {texture.Width}x{texture.Height}"
                            );
                        }
                        catch (Exception ex)
                        {
                            Logger.Warn(
                                $"Erro ao criar textura do calendário: {ex.Message}"
                            );

                            _calendarStatus.Text =
                                "Última atualização: erro ao carregar calendário.";
                        }
                        finally
                        {
                            imageStream.Dispose();
                        }
                    }
                );
            }
            catch (Exception ex)
            {
                Logger.Warn(
                    $"Erro ao processar configuração do calendário: {ex.Message}"
                );

                _calendarPeriod.Text =
                    "Período: não disponível";

                _calendarStatus.Text =
                    "Última atualização: não foi possível carregar o calendário.";
            }
        }

        // ============================================================
        // MENSAGEM DO DIA
        // ============================================================

        private void ShowDailyMessageWindow()
        {
            if (_guildResourcesWindow == null)
            {
                return;
            }

            _guildResourcesWindow.Show();
            SetGuildTab(1);
            _ = LoadDailyMessageAsync();
        }

        private void CreateDailyMessageWindow()
        {
            // A Mensagem do Dia agora faz parte da janela de Recursos da Guild.
            // O método é mantido apenas para compatibilidade com a estrutura
            // anterior do módulo.
        }

        private async Task LoadDailyMessageAsync()
        {
            try
            {

                string json = null;
                Exception lastException = null;

                const int maxAttempts = 3;

                for (int attempt = 1; attempt <= maxAttempts; attempt++)
                {
                    try
                    {
                        Logger.Info(
                            $"Tentativa {attempt}/{maxAttempts} para carregar a Mensagem do Dia..."
                        );

                        json =
                            await _httpClient.GetStringAsync(
                                ConfigUrl
                            );

                        break;
                    }
                    catch (Exception ex)
                    {
                        lastException = ex;

                        Logger.Warn(
                            $"Falha ao carregar a Mensagem do Dia na tentativa {attempt}/{maxAttempts}: {ex.Message}"
                        );

                        if (attempt < maxAttempts)
                        {
                            await Task.Delay(
                                TimeSpan.FromSeconds(attempt)
                            );
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(json))
                {
                    throw new Exception(
                        $"Não foi possível carregar o config.json: {lastException?.Message}"
                    );
                }

                var config =
                    JsonConvert.DeserializeObject<RemoteConfig>(
                        json
                    );

                if (
                    config?.msgDia == null ||
                    config.msgDia.Count == 0
                )
                {
                    throw new Exception(
                        "Nenhuma imagem foi encontrada em msgDia."
                    );
                }

                string today =
                    GetBrazilTime().ToString(
                        "yyyy-MM-dd"
                    );

                int selectedImageNumber =
                    GetDailyMessageImageNumber(
                        today,
                        config.msgDia
                    );

                string imageUrl = null;

                if (
                    !config.msgDia.TryGetValue(
                        selectedImageNumber.ToString(),
                        out imageUrl
                    ) ||
                    string.IsNullOrWhiteSpace(imageUrl)
                )
                {
                    // Caso o número salvo não exista mais no config,
                    // escolhe uma nova imagem para o dia.
                    selectedImageNumber =
                        SelectNewDailyMessageImage(
                            config.msgDia
                        );

                    imageUrl =
                        config.msgDia[
                            selectedImageNumber.ToString()
                        ];

                    _dailyMessageDateSetting.Value =
                        today;

                    _dailyMessageImageSetting.Value =
                        selectedImageNumber;
                }

                Logger.Info(
                    $"Mensagem do Dia selecionada: {selectedImageNumber} - {imageUrl}"
                );

                byte[] imageBytes = null;
                Exception lastImageException = null;

                for (int attempt = 1; attempt <= maxAttempts; attempt++)
                {
                    try
                    {
                        Logger.Info(
                            $"Tentativa {attempt}/{maxAttempts} para carregar a imagem da Mensagem do Dia..."
                        );

                        imageBytes =
                            await _httpClient.GetByteArrayAsync(
                                imageUrl
                            );

                        break;
                    }
                    catch (Exception ex)
                    {
                        lastImageException = ex;

                        Logger.Warn(
                            $"Falha ao carregar imagem da Mensagem do Dia na tentativa {attempt}/{maxAttempts}: {ex.Message}"
                        );

                        if (attempt < maxAttempts)
                        {
                            await Task.Delay(
                                TimeSpan.FromSeconds(attempt)
                            );
                        }
                    }
                }

                if (
                    imageBytes == null ||
                    imageBytes.Length == 0
                )
                {
                    throw new Exception(
                        $"Não foi possível carregar a imagem: {lastImageException?.Message}"
                    );
                }

                var imageStream =
                    new System.IO.MemoryStream(
                        imageBytes
                    );

                GameService.Graphics.QueueMainThreadRender(
                    graphicsDevice =>
                    {
                        try
                        {
                            var texture =
                                Texture2D.FromStream(
                                    graphicsDevice,
                                    imageStream
                                );

                            _dailyMessageRawTexture?.Dispose();

                            _dailyMessageRawTexture =
                                texture;

                            _dailyMessageTexture =
                                new AsyncTexture2D(
                                    texture
                                );

                            _dailyMessageImage.Texture =
                                _dailyMessageTexture;

                            FitDailyMessageImage();

                        }
                        catch (Exception ex)
                        {
                            Logger.Warn(
                                $"Erro ao criar textura da Mensagem do Dia: {ex.Message}"
                            );

                        }
                        finally
                        {
                            imageStream.Dispose();
                        }
                    }
                );
            }
            catch (Exception ex)
            {
                Logger.Warn(
                    $"Erro ao carregar a Mensagem do Dia: {ex.Message}"
                );

            }
        }

        private int GetDailyMessageImageNumber(
            string today,
            Dictionary<string, string> messages)
        {
            int savedImage =
                _dailyMessageImageSetting?.Value ?? 0;

            string savedDate =
                _dailyMessageDateSetting?.Value ?? string.Empty;

            // A escolha de hoje já foi registrada: mantém exatamente
            // a mesma imagem durante todo o dia.
            if (
                savedDate == today &&
                savedImage > 0 &&
                messages.ContainsKey(
                    savedImage.ToString()
                )
            )
            {
                return savedImage;
            }

            int selectedImage =
                SelectNewDailyMessageImage(
                    messages
                );

            _dailyMessageDateSetting.Value =
                today;

            _dailyMessageImageSetting.Value =
                selectedImage;

            return selectedImage;
        }

        private int SelectNewDailyMessageImage(
            Dictionary<string, string> messages)
        {
            var validKeys =
                new List<int>();

            foreach (string key in messages.Keys)
            {
                int number;

                if (
                    int.TryParse(
                        key,
                        out number
                    ) &&
                    number > 0 &&
                    !string.IsNullOrWhiteSpace(
                        messages[key]
                    )
                )
                {
                    validKeys.Add(number);
                }
            }

            if (validKeys.Count == 0)
            {
                throw new Exception(
                    "msgDia não possui nenhuma chave numérica válida."
                );
            }

            int previousImage =
                _dailyMessageImageSetting?.Value ?? 0;

            // Evita repetir a mensagem anterior quando houver
            // mais de uma imagem disponível.
            var availableKeys =
                validKeys.FindAll(
                    number =>
                        validKeys.Count == 1 ||
                        number != previousImage
                );

            int index =
                _dailyMessageRandom.Next(
                    availableKeys.Count
                );

            return availableKeys[index];
        }

        private void FitDailyMessageImage()
        {
            if (
                _calendarWindow == null ||
                _dailyMessageImage == null
            )
            {
                return;
            }

            // As artes da Mensagem do Dia são padronizadas em 800x800
            // e são exibidas sempre em 600x600.
            const int imageSize = 600;

            int availableWidth =
                Math.Max(
                    1,
                    _calendarWindow.ContentRegion.Width -
                    CalendarContentLeft
                );

            int x =
                CalendarContentLeft +
                Math.Max(
                    0,
                    (availableWidth - imageSize) / 2
                );

            int y =
                Math.Max(
                    10,
                    (
                        _calendarWindow.ContentRegion.Height -
                        imageSize
                    ) / 2
                );

            _dailyMessageImage.Size =
                new Point(
                    imageSize,
                    imageSize
                );

            _dailyMessageImage.Location =
                new Point(
                    x,
                    y
                );
        }

        // ============================================================
        // RECURSOS DA STAFF
        // ============================================================

        private void ShowStaffPinWindow()
        {
            if (_staffPinWindow == null)
            {
                CreateStaffPinWindow();
            }

            _staffPinInput.Text = string.Empty;
            _staffPinStatus.Text = string.Empty;
            _staffPinWindow.Visible = true;
            _staffPinInput.Focused = true;
        }

        private void CreateStaffPinWindow()
        {
            // Modal realmente compacta: não utiliza StandardWindow,
            // pois a moldura do StandardWindow possui uma área visual
            // muito maior que o conteúdo necessário.
            const int windowWidth = 360;
            const int windowHeight = 210;

            int screenWidth =
                GameService.Graphics.SpriteScreen.Width;

            int screenHeight =
                GameService.Graphics.SpriteScreen.Height;

            var windowBackground =
                AsyncTexture2D.FromAssetId(155985);

            _staffPinWindow =
                new Panel
                {
                    Location =
                        new Point(
                            Math.Max(
                                0,
                                (screenWidth - windowWidth) / 2
                            ),
                            Math.Max(
                                0,
                                (screenHeight - windowHeight) / 2
                            )
                        ),

                    Size =
                        new Point(
                            windowWidth,
                            windowHeight
                        ),

                    BackgroundTexture =
                        windowBackground,

                    Parent =
                        GameService.Graphics.SpriteScreen,

                    ShowBorder =
                        false,

                    ZIndex =
                        100
                };

            _staffPinTitle =
                new Label
                {
                    Text =
                        "Recursos da Staff",

                    Location =
                        new Point(
                            58,
                            18
                        ),

                    AutoSizeWidth =
                        true,

                    AutoSizeHeight =
                        true,

                    Parent =
                        _staffPinWindow,

                    Font =
                        GameService.Content.DefaultFont18,

                    TextColor =
                        Color.White,

                    StrokeText =
                        false,

                    ZIndex =
                        101
                };

            _staffPinSubtitle =
                new Label
                {
                    Text =
                        "Área restrita",

                    Location =
                        new Point(
                            58,
                            43
                        ),

                    AutoSizeWidth =
                        true,

                    AutoSizeHeight =
                        true,

                    Parent =
                        _staffPinWindow,

                    Font =
                        GameService.Content.DefaultFont14,

                    TextColor =
                        Color.LightGray,

                    StrokeText =
                        false,

                    ZIndex =
                        101
                };

            var emblem =
                new Image
                {
                    Texture =
                        _iconTexture,

                    Location =
                        new Point(
                            20,
                            15
                        ),

                    Size =
                        new Point(
                            30,
                            30
                        ),

                    Parent =
                        _staffPinWindow,

                    ZIndex =
                        102
                };

            _staffPinCloseButton =
                new StandardButton
                {
                    Text =
                        "X",

                    Location =
                        new Point(
                            windowWidth - 42,
                            16
                        ),

                    Size =
                        new Point(
                            25,
                            25
                        ),

                    Parent =
                        _staffPinWindow,

                    ZIndex =
                        102
                };

            _staffPinCloseButton.Click +=
                (sender, e) =>
                {
                    _staffPinWindow.Visible = false;
                };

            var title =
                new Label
                {
                    Text =
                        "Digite o PIN da Staff",

                    Location =
                        new Point(
                            25,
                            70
                        ),

                    AutoSizeWidth =
                        true,

                    AutoSizeHeight =
                        true,

                    Parent =
                        _staffPinWindow,

                    Font =
                        GameService.Content.DefaultFont18,

                    TextColor =
                        Color.White,

                    StrokeText =
                        false,

                    ZIndex =
                        101
                };

            var description =
                new Label
                {
                    Text =
                        "Para habilitar as ferramentas administrativas.",

                    Location =
                        new Point(
                            25,
                            99
                        ),

                    AutoSizeWidth =
                        false,

                    Width =
                        310,

                    AutoSizeHeight =
                        true,

                    Parent =
                        _staffPinWindow,

                    Font =
                        GameService.Content.DefaultFont14,

                    TextColor =
                        Color.LightGray,

                    StrokeText =
                        false,

                    ZIndex =
                        101
                };

            _staffPinInput =
                new TextBox
                {
                    Location =
                        new Point(
                            25,
                            130
                        ),

                    Width =
                        200,

                    Parent =
                        _staffPinWindow,

                    ZIndex =
                        101
                };

            var validateButton =
                new StandardButton
                {
                    Text =
                        "Validar PIN",

                    Location =
                        new Point(
                            235,
                            128
                        ),

                    Size =
                        new Point(
                            100,
                            32
                        ),

                    Parent =
                        _staffPinWindow,

                    ZIndex =
                        101
                };

            _staffPinStatus =
                new Label
                {
                    Text =
                        string.Empty,

                    Location =
                        new Point(
                            25,
                            170
                        ),

                    AutoSizeWidth =
                        false,

                    Width =
                        310,

                    AutoSizeHeight =
                        true,

                    Parent =
                        _staffPinWindow,

                    Font =
                        GameService.Content.DefaultFont14,

                    TextColor =
                        new Color(
                            255,
                            100,
                            100
                        ),

                    StrokeText =
                        false,

                    ZIndex =
                        101
                };

            validateButton.Click +=
                (sender, e) =>
                {
                    ValidateStaffPin();
                };

            _staffPinInput.EnterPressed +=
                (sender, e) =>
                {
                    ValidateStaffPin();
                };
        }

        private void ValidateStaffPin()
        {
            if (_staffPinInput == null)
            {
                return;
            }

            if (_staffPinInput.Text == StaffPin)
            {
                _staffResourcesEnabled = true;

                _staffPinStatus.Text =
                    "PIN válido. Recursos da Staff habilitados.";

                _staffPinStatus.TextColor =
                    new Color(
                        120,
                        220,
                        120
                    );

                _staffPinWindow.Visible = false;

                ShowStaffResourcesWindow();
            }
            else
            {
                _staffPinStatus.Text =
                    "PIN inválido. Tente novamente.";

                _staffPinStatus.TextColor =
                    new Color(
                        255,
                        100,
                        100
                    );

                _staffPinInput.Text = string.Empty;
                _staffPinInput.Focused = true;
            }
        }

        // ============================================================
        // RECURSOS DA STAFF / ABAS LATERAIS
        // ============================================================

        private void ShowStaffResourcesWindow()
        {
            if (_staffResourcesWindow == null)
            {
                CreateStaffResourcesWindow();
            }

            _staffResourcesWindow.Show();
            SetStaffTab(0);
        }

        private void CreateStaffResourcesWindow()
        {
            int screenWidth =
                GameService.Graphics.SpriteScreen.Width;

            int screenHeight =
                GameService.Graphics.SpriteScreen.Height;

            const int windowWidth = 1000;
            const int windowHeight = 620;

            var windowBackground =
                AsyncTexture2D.FromAssetId(155985);

            _staffResourcesWindow =
                new TabbedWindow2(
                    windowBackground,
                    new Rectangle(
                        40,
                        26,
                        913,
                        691
                    ),
                    new Rectangle(
                        90,
                        71,
                        819,
                        605
                    ),
                    new Point(
                        windowWidth,
                        windowHeight
                    ))
                {
                    Parent =
                        GameService.Graphics.SpriteScreen,

                    Title =
                        "Recursos da Staff",

                    Subtitle =
                        "Sociedade do Dragão [BR]",

                    Emblem =
                        _iconTexture,

                    Location =
                        new Point(
                            Math.Max(
                                0,
                                (screenWidth - windowWidth) / 2
                            ),
                            Math.Max(
                                0,
                                (screenHeight - windowHeight) / 2
                            )
                        ),

                    CanResize =
                        false,

                    SavesPosition =
                        true,

                    Id =
                        "SociedadeDoDragao_RecursosStaff"
                };

            _staffResourcesWindow.Hidden +=
                (sender, e) =>
                {
                    ClearRaffleData();
                };

            // Os conteúdos das abas são criados como painéis independentes.
            // O TabbedWindow2 os hospeda automaticamente através do IView.
            CreateQuickMessagesWindow();
            CreateRaffleWindow();

            _staffRaffleView = _staffRaffleContent;
            _staffQuickMessagesView = _staffQuickMessagesContent;

            _staffRaffleTab =
                new Tab(
                    _iconTexture,
                    () => new PanelView(_staffRaffleView),
                    "Sorteador"
                );

            _staffQuickMessagesTab =
                new Tab(
                    _iconTexture,
                    () => new PanelView(_staffQuickMessagesView),
                    "Mensagens Rápidas"
                );

            _staffResourcesWindow.Tabs.Add(_staffRaffleTab);
            _staffResourcesWindow.Tabs.Add(_staffQuickMessagesTab);

            _staffResourcesWindow.TabChanged +=
                (sender, e) =>
                {
                    if (e.NewValue == _staffQuickMessagesTab)
                    {
                        _ = LoadQuickMessagesAsync();
                    }
                };

            _staffResourcesWindow.Resized +=
                (sender, e) =>
                {
                    ResizeTabViewPanels();
                    CenterRaffleResultLabels();
                };

            _staffResourcesWindow.SelectedTab = _staffRaffleTab;
        }

        private void SetStaffTab(int tabIndex)
        {
            if (_staffResourcesWindow == null)
                return;

            _staffResourcesWindow.SelectedTab =
                tabIndex == 0
                    ? _staffRaffleTab
                    : _staffQuickMessagesTab;
        }

        // ============================================================
        // MENSAGENS RÁPIDAS
        // ============================================================

        private void ShowQuickMessagesWindow()
        {
            ShowStaffResourcesWindow();

            if (_staffResourcesWindow != null)
            {
                SetStaffTab(1);
                _ = LoadQuickMessagesAsync();
            }
        }

        private async Task LoadQuickMessagesAsync()
        {
            try
            {
                string json =
                    await _httpClient.GetStringAsync(
                        ConfigUrl
                    );

                var config =
                    JsonConvert.DeserializeObject<RemoteConfig>(
                        json
                    );

                if (config?.mensagens != null)
                {
                    _quickMessageRecruitment =
                        config.mensagens.msgRecrutamento ?? string.Empty;

                    _quickMessageGuildMission =
                        config.mensagens.msgGuildMission ?? string.Empty;

                    _quickMessageReset =
                        config.mensagens.msgReset ?? string.Empty;

                    UpdateQuickMessagesContent();
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(
                    $"Não foi possível carregar as mensagens rápidas: {ex.Message}"
                );
            }
        }

        private void CreateQuickMessagesWindow()
        {
            if (_staffResourcesWindow == null)
            {
                return;
            }

            _staffQuickMessagesContent =
                new Panel
                {
                    Location =
                        Point.Zero,

                    Size =
                        _staffResourcesWindow.ContentRegion.Size,

                    Parent =
                        null,

                    ShowBorder =
                        false,

                    ZIndex =
                        5
                };

            CreateQuickMessageRow(
                "Recrutamento",
                20,
                15,
                out _quickMessageRecruitmentLabel,
                () => CopyToClipboard(_quickMessageRecruitment)
            );

            CreateQuickMessageRow(
                "Guild Mission",
                20,
                170,
                out _quickMessageGuildMissionLabel,
                () => CopyToClipboard(_quickMessageGuildMission)
            );

            CreateQuickMessageRow(
                "Reset",
                20,
                325,
                out _quickMessageResetLabel,
                () => CopyToClipboard(_quickMessageReset)
            );
        }

        private void CreateQuickMessageRow(
            string title,
            int x,
            int y,
            out Label messageLabel,
            Func<bool> copyAction)
        {
            new Label
            {
                Text = title,
                Location = new Point(x, y),
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Parent = _staffQuickMessagesContent,
                Font = GameService.Content.DefaultFont18,
                TextColor = Color.White,
                StrokeText = false,
                ZIndex = 20
            };

            var messagePanel =
                new Panel
                {
                    Location = new Point(x, y + 30),
                    Size = new Point(590, 100),
                    Parent = _staffQuickMessagesContent,
                    BackgroundColor = Color.FromNonPremultiplied(245, 245, 245, 255),
                    ShowBorder = true,
                    ZIndex = 20
                };

            messageLabel =
                new Label
                {
                    Text = string.Empty,
                    Location = new Point(10, 8),
                    Width = 570,
                    Height = 84,
                    AutoSizeWidth = false,
                    AutoSizeHeight = false,
                    WrapText = true,
                    Parent = messagePanel,
                    Font = GameService.Content.DefaultFont14,
                    TextColor = new Color(70, 70, 70),
                    StrokeText = false,
                    ZIndex = 21
                };

            var copyButton =
                new StandardButton
                {
                    Text = "Copiar",
                    Location = new Point(x + 605, y + 30),
                    Size = new Point(100, 48),
                    Parent = _staffQuickMessagesContent,
                    ZIndex = 20
                };

            var copiedLabel =
                new Label
                {
                    Text = string.Empty,
                    Location = new Point(x + 628, y + 82),
                    AutoSizeWidth = true,
                    AutoSizeHeight = true,
                    Parent = _staffQuickMessagesContent,
                    Font = GameService.Content.DefaultFont14,
                    TextColor = new Color(255, 225, 0),
                    StrokeText = false,
                    ZIndex = 20
                };

            copyButton.Click +=
                (sender, e) =>
                {
                    if (copyAction())
                    {
                        ShowCopyFeedback(copiedLabel);
                    }
                };
        }

        private async void ShowCopyFeedback(Label feedbackLabel)
        {
            if (feedbackLabel == null)
            {
                return;
            }

            feedbackLabel.Text = "Copiado!";

            await Task.Delay(2000);

            if (feedbackLabel == null)
            {
                return;
            }

            GameService.Graphics.QueueMainThreadRender(
                graphicsDevice =>
                {
                    if (feedbackLabel != null)
                    {
                        feedbackLabel.Text = string.Empty;
                    }
                }
            );
        }

        private void UpdateQuickMessagesContent()
        {
            if (_quickMessageRecruitmentLabel != null)
            {
                _quickMessageRecruitmentLabel.Text =
                    _quickMessageRecruitment;
            }

            if (_quickMessageGuildMissionLabel != null)
            {
                _quickMessageGuildMissionLabel.Text =
                    _quickMessageGuildMission;
            }

            if (_quickMessageResetLabel != null)
            {
                _quickMessageResetLabel.Text =
                    _quickMessageReset;
            }
        }

        private static bool CopyToClipboard(string text)
        {
            if (text == null)
            {
                text = string.Empty;
            }

            const uint CF_UNICODETEXT = 13;
            const uint GMEM_MOVEABLE = 0x0002;

            try
            {
                for (int attempt = 0; attempt < 10; attempt++)
                {
                    if (!OpenClipboard(IntPtr.Zero))
                    {
                        Thread.Sleep(20);
                        continue;
                    }

                    try
                    {
                        if (!EmptyClipboard())
                        {
                            return false;
                        }

                        int bytes =
                            (text.Length + 1) * 2;

                        IntPtr hGlobal =
                            GlobalAlloc(
                                GMEM_MOVEABLE,
                                (UIntPtr)bytes
                            );

                        if (hGlobal == IntPtr.Zero)
                        {
                            return false;
                        }

                        IntPtr target =
                            GlobalLock(hGlobal);

                        if (target == IntPtr.Zero)
                        {
                            GlobalFree(hGlobal);
                            return false;
                        }

                        try
                        {
                            Marshal.Copy(
                                text.ToCharArray(),
                                0,
                                target,
                                text.Length
                            );

                            Marshal.WriteInt16(
                                target,
                                text.Length * 2,
                                0
                            );
                        }
                        finally
                        {
                            GlobalUnlock(hGlobal);
                        }

                        if (
                            SetClipboardData(
                                CF_UNICODETEXT,
                                hGlobal
                            ) == IntPtr.Zero
                        )
                        {
                            GlobalFree(hGlobal);
                            return false;
                        }

                        Logger.Info(
                            "Mensagem rápida copiada para o clipboard."
                        );

                        return true;
                    }
                    finally
                    {
                        CloseClipboard();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(
                    $"Erro ao copiar mensagem para o clipboard: {ex.Message}"
                );
            }

            return false;
        }

        // ============================================================
        // SORTEADOR DE PARTICIPANTES
        // ============================================================

        private void ShowRaffleWindow()
        {
            ShowStaffResourcesWindow();

            if (_staffResourcesWindow != null)
            {
                SetStaffTab(0);
            }
        }

        private void CreateRaffleWindow()
        {
            if (_staffResourcesWindow == null)
            {
                return;
            }

            _staffRaffleContent =
                new Panel
                {
                    Location =
                        Point.Zero,

                    Size =
                        _staffResourcesWindow.ContentRegion.Size,

                    Parent =
                        null,

                    ShowBorder =
                        false,

                    ZIndex =
                        5
                };

            // --------------------------------------------------------
            // GRADE DE PARTICIPANTES
            // --------------------------------------------------------

            const int columns = 3;

            const int startX = 20;
            const int startY = 18;

            const int columnWidth = 235;
            const int rowHeight = 55;

            for (int i = 0; i < _raffleInputs.Length; i++)
            {
                int column = i % columns;
                int row = i / columns;

                int x =
                    startX +
                    column * columnWidth;

                int y =
                    startY +
                    row * rowHeight;

                var numberLabel =
                    new Label
                    {
                        Text =
                            $"#{i + 1}",

                        Location =
                            new Point(
                                x,
                                y + 7
                            ),

                        AutoSizeWidth =
                            true,

                        AutoSizeHeight =
                            true,

                        Parent =
                            _staffRaffleContent,

                        Font =
                            GameService.Content.DefaultFont14,

                        TextColor =
                            Color.LightGray,

                        StrokeText =
                            false,

                        ZIndex =
                            20
                    };

                _raffleInputs[i] =
                    new TextBox
                    {
                        Location =
                            new Point(
                                x + 32,
                                y
                            ),

                        Width =
                            180,

                        Parent =
                            _staffRaffleContent,

                        ZIndex =
                            20
                    };

                _raffleDisabledOverlays[i] =
                    new Panel
                    {
                        Location =
                            new Point(
                                x + 32 + 95,
                                y
                            ),

                        Size =
                            new Point(
                                82,
                                32
                            ),

                        Parent =
                            _staffRaffleContent,

                        BackgroundColor =
                            Color.FromNonPremultiplied(
                                20,
                                20,
                                20,
                                145
                            ),

                        ShowBorder =
                            false,

                        Visible =
                            false,

                        ZIndex =
                            30
                    };

                _raffleDisabledLabels[i] =
                    new Label
                    {
                        Text =
                            "SORTEADO",

                        Location =
                            new Point(
                                4,
                                8
                            ),

                        AutoSizeWidth =
                            true,

                        AutoSizeHeight =
                            true,

                        Parent =
                            _raffleDisabledOverlays[i],

                        Font =
                            GameService.Content.DefaultFont14,

                        TextColor =
                            new Color(
                                255,
                                225,
                                0
                            ),

                        StrokeText =
                            false,

                        ZIndex =
                            31
                    };
            }

            // --------------------------------------------------------
            // RESULTADO
            // --------------------------------------------------------

            _raffleResultLabel =
                new Label
                {
                    Text =
                        "Sorteado:",

                    Location =
                        new Point(
                            0,
                            330
                        ),

                    AutoSizeWidth =
                        true,

                    AutoSizeHeight =
                        true,

                    Parent =
                        _staffRaffleContent,

                    Font =
                        GameService.Content.DefaultFont18,

                    TextColor =
                        Color.White,

                    StrokeText =
                        false,

                    ZIndex =
                        20
                };

            _raffleWinnerLabel =
                new Label
                {
                    Text =
                        "—",

                    Location =
                        new Point(
                            0,
                            356
                        ),

                    AutoSizeWidth =
                        true,

                    AutoSizeHeight =
                        true,

                    Parent =
                        _staffRaffleContent,

                    Font =
                        GameService.Content.DefaultFont18,

                    TextColor =
                        new Color(
                            255,
                            225,
                            0
                        ),

                    StrokeText =
                        false,

                    ZIndex =
                        20
                };

            CenterRaffleResultLabels();

            // --------------------------------------------------------
            // BOTÕES
            // --------------------------------------------------------

            const int raffleButtonsY = 405;

            int raffleButtonsStartX =
                Math.Max(
                    0,
                    (
                        _staffRaffleContent.Width -
                        250
                    ) / 2
                );

            _raffleDrawButton =
                new StandardButton
                {
                    Text =
                        "SORTEAR",

                    Location =
                        new Point(
                            raffleButtonsStartX,
                            raffleButtonsY
                        ),

                    Size =
                        new Point(
                            120,
                            34
                        ),

                    Parent =
                        _staffRaffleContent,

                    ZIndex =
                        20
                };

            _raffleResetButton =
                new StandardButton
                {
                    Text =
                        "REINICIAR",

                    Location =
                        new Point(
                            raffleButtonsStartX + 130,
                            raffleButtonsY
                        ),

                    Size =
                        new Point(
                            120,
                            34
                        ),

                    Parent =
                        _staffRaffleContent,

                    ZIndex =
                        20
                };

            _raffleDrawButton.Click +=
                (sender, e) =>
                {
                    DrawParticipant();
                };

            _raffleResetButton.Click +=
                (sender, e) =>
                {
                    ResetRaffle();
                };
        }

        private void CenterRaffleResultLabels()
        {
            if (_raffleWindow == null)
            {
                return;
            }

            int centerX =
                _staffRaffleContent != null
                    ? _staffRaffleContent.Width / 2
                    : 0;

            if (_raffleResultLabel != null)
            {
                _raffleResultLabel.Location =
                    new Point(
                        centerX -
                        _raffleResultLabel.Width / 2,
                        330
                    );
            }

            if (_raffleWinnerLabel != null)
            {
                _raffleWinnerLabel.Location =
                    new Point(
                        centerX -
                        _raffleWinnerLabel.Width / 2,
                        356
                    );
            }
        }

        private void DrawParticipant()
        {
            var eligibleIndexes =
                new System.Collections.Generic.List<int>();

            for (int i = 0; i < _raffleInputs.Length; i++)
            {
                if (
                    !_raffleWinnerDisabled[i] &&
                    _raffleInputs[i] != null &&
                    !string.IsNullOrWhiteSpace(
                        _raffleInputs[i].Text
                    )
                )
                {
                    eligibleIndexes.Add(i);
                }
            }

            if (eligibleIndexes.Count == 0)
            {
                _raffleResultLabel.Text =
                    "Sorteado:";

                _raffleWinnerLabel.Text =
                    "Nenhum participante disponível.";

                CenterRaffleResultLabels();

                return;
            }

            int randomPosition =
                _raffleRandom.Next(
                    eligibleIndexes.Count
                );

            int selectedIndex =
                eligibleIndexes[randomPosition];

            string winner =
                _raffleInputs[selectedIndex].Text.Trim();

            _raffleWinnerDisabled[selectedIndex] =
                true;

            _raffleInputs[selectedIndex].Enabled =
                false;

            _raffleInputs[selectedIndex].ForeColor =
                Color.LightGray;

            if (_raffleDisabledOverlays[selectedIndex] != null)
            {
                _raffleDisabledOverlays[selectedIndex].Visible = true;
            }

            _raffleResultLabel.Text =
                "Sorteado:";

            _raffleWinnerLabel.Text =
                winner;

            CenterRaffleResultLabels();

        }

        private void ResetRaffle()
        {
            for (int i = 0; i < _raffleInputs.Length; i++)
            {
                _raffleWinnerDisabled[i] =
                    false;

                if (_raffleInputs[i] != null)
                {
                    _raffleInputs[i].Enabled =
                        true;

                    // Volta ao estado visual padrão do TextBox.
                    // Não definimos BackgroundColor aqui para não alterar
                    // a aparência original do controle.
                    _raffleInputs[i].ForeColor =
                        Color.White;
                }

                if (_raffleDisabledOverlays[i] != null)
                {
                    _raffleDisabledOverlays[i].Visible =
                        false;
                }
            }

            _raffleResultLabel.Text =
                "Sorteado:";

            _raffleWinnerLabel.Text =
                "—";

            CenterRaffleResultLabels();

        }

        private void ClearRaffleData()
        {
            for (int i = 0; i < _raffleInputs.Length; i++)
            {
                _raffleWinnerDisabled[i] =
                    false;

                if (_raffleInputs[i] != null)
                {
                    _raffleInputs[i].Text =
                        string.Empty;

                    _raffleInputs[i].Enabled =
                        true;

                    _raffleInputs[i].ForeColor =
                        Color.White;
                }

                if (_raffleDisabledOverlays[i] != null)
                {
                    _raffleDisabledOverlays[i].Visible =
                        false;
                }
            }

            if (_raffleResultLabel != null)
            {
                _raffleResultLabel.Text =
                    "Sorteado:";
            }

            if (_raffleWinnerLabel != null)
            {
                _raffleWinnerLabel.Text =
                    "—";
            }

            CenterRaffleResultLabels();

        }

        [DllImport(
            "user32.dll",
            SetLastError = true
        )]
        private static extern bool OpenClipboard(
            IntPtr hWndNewOwner
        );

        [DllImport(
            "user32.dll",
            SetLastError = true
        )]
        private static extern bool CloseClipboard();

        [DllImport(
            "user32.dll",
            SetLastError = true
        )]
        private static extern bool EmptyClipboard();

        [DllImport(
            "user32.dll",
            SetLastError = true
        )]
        private static extern IntPtr SetClipboardData(
            uint uFormat,
            IntPtr hMem
        );

        [DllImport(
            "kernel32.dll",
            SetLastError = true
        )]
        private static extern IntPtr GlobalAlloc(
            uint uFlags,
            UIntPtr dwBytes
        );

        [DllImport(
            "kernel32.dll",
            SetLastError = true
        )]
        private static extern IntPtr GlobalLock(
            IntPtr hMem
        );

        [DllImport(
            "kernel32.dll",
            SetLastError = true
        )]
        private static extern bool GlobalUnlock(
            IntPtr hMem
        );

        [DllImport(
            "kernel32.dll",
            SetLastError = true
        )]
        private static extern IntPtr GlobalFree(
            IntPtr hMem
        );

        private sealed class PanelView : IView
        {
            private readonly Panel _panel;

            public event EventHandler<EventArgs> Loaded;
            public event EventHandler<EventArgs> Built;
            public event EventHandler<EventArgs> Unloaded;

            public PanelView(Panel panel)
            {
                _panel = panel;
            }

            public Task<bool> DoLoad(IProgress<string> progress)
            {
                return Task.FromResult(_panel != null);
            }

            public void DoBuild(Container buildPanel)
            {
                if (_panel == null || buildPanel == null)
                    return;

                _panel.Parent = buildPanel;
                _panel.Location = Point.Zero;
                _panel.Size = buildPanel.ContentRegion.Size;

                Built?.Invoke(this, EventArgs.Empty);
                Loaded?.Invoke(this, EventArgs.Empty);
            }

            public void DoUnload()
            {
                if (_panel != null)
                    _panel.Parent = null;

                Unloaded?.Invoke(this, EventArgs.Empty);
            }
        }

        // ============================================================
        // DESCARREGAR
        // ============================================================

        protected override void Unload()
        {
            _isDragging = false;

            _currentTimeTimer?.Dispose();

            _cornerIcon?.Dispose();
            _guildMenu?.Dispose();
            _staffResourcesWindow?.Dispose();
            _guildResourcesWindow?.Dispose();
            _calendarTexture?.Dispose();
            _calendarTabIcon?.Dispose();
            _iconTexture?.Dispose();
            _httpClient?.Dispose();

            _cornerIcon = null;
            _guildMenu = null;
            _guildResourcesWindow = null;
            _staffResourcesWindow = null;
            _guildTabSidebar = null;
            _guildCalendarTab = null;
            _guildDailyMessageTab = null;
            _guildCalendarTabLabel = null;
            _guildDailyMessageTabLabel = null;
            _guildCalendarView = null;
            _guildDailyMessageView = null;
            _staffTabSidebar = null;
            _staffRaffleTab = null;
            _staffQuickMessagesTab = null;
            _staffRaffleContent = null;
            _staffQuickMessagesContent = null;
            _staffRaffleTabLabel = null;
            _staffQuickMessagesTabLabel = null;
            _staffRaffleView = null;
            _staffQuickMessagesView = null;
            _calendarWindow = null;
            _calendarViewport = null;
            _weekdayColumn = null;
            _todayHighlight = null;
            _calendarImage = null;
            _currentTimeLine = null;
            _currentTimeLabel = null;
            _currentTimeLineTexture = null;
            _currentTimeLineRawTexture?.Dispose();
            _currentTimeLineRawTexture = null;
            _currentTimeTimer = null;
            _calendarTitle = null;
            _calendarTitleTexture?.Dispose();
            _calendarTitleTexture = null;
            _calendarPeriod = null;
            _calendarStatus = null;
            _calendarWarning = null;
            _staffPinInput = null;
            _staffPinStatus = null;
            _staffPinTitle = null;
            _staffPinSubtitle = null;
            _staffPinCloseButton = null;
            _staffPinWindow = null;
            _staffResourcesEnabled = false;
            _quickMessagesWindow = null;
            _quickMessageRecruitmentLabel = null;
            _quickMessageGuildMissionLabel = null;
            _quickMessageResetLabel = null;
            _quickMessageRecruitment = string.Empty;
            _quickMessageGuildMission = string.Empty;
            _quickMessageReset = string.Empty;
            _dailyMessageWindow = null;
            _dailyMessageImage = null;
            _dailyMessageTexture = null;
            _dailyMessageRawTexture?.Dispose();
            _dailyMessageRawTexture = null;
            _dailyMessageDateSetting = null;
            _dailyMessageImageSetting = null;
            _raffleWindow = null;
            _raffleResultLabel = null;
            _raffleWinnerLabel = null;
            _raffleDrawButton = null;
            _raffleResetButton = null;

            for (int i = 0; i < _raffleDisabledOverlays.Length; i++)
            {
                _raffleDisabledOverlays[i] = null;
                _raffleDisabledLabels[i] = null;
            }

            _calendarTexture = null;
            _iconTexture = null;

            _calendarOriginalWidth = 0;
            _calendarOriginalHeight = 0;
            _calendarScale = 1.0f;
            _calendarUserHasDragged = false;

            Logger.Info(
                "Sociedade do Dragão descarregado!"
            );
        }
    }
}

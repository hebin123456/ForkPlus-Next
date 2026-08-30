using System;
using ForkPlus.UI.WpfCompat;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Markup;
using ForkPlus.UI.Helpers;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace ForkPlus.UI
{
	[global::Avalonia.Controls.Metadata.TemplatePartAttribute(Name = "PART_MinimizeButton", Type = typeof(Button))]
	[global::Avalonia.Controls.Metadata.TemplatePartAttribute(Name = "PART_MaximizeButton", Type = typeof(Button))]
	[global::Avalonia.Controls.Metadata.TemplatePartAttribute(Name = "PART_RestoreButton", Type = typeof(Button))]
	[global::Avalonia.Controls.Metadata.TemplatePartAttribute(Name = "PART_CloseButton", Type = typeof(Button))]
	[global::Avalonia.Controls.Metadata.TemplatePartAttribute(Name = "PART_WindowHeader", Type = typeof(global::Avalonia.Controls.Control))]
	public class CustomWindow : Window
	{
		// TODO 迁移（根因修复）：Avalonia 的 implicit ControlTheme 查找用 StyleKey（Window 基类
		// StyleKeyOverride=typeof(Window)），而 Window.axaml 的 ControlTheme key 是 {x:Type ui:CustomWindow}，
		// 二者不匹配导致隐式主题永不应用、模板退化为 ContentControl 默认 FuncControlTemplate
		// （PART_MainMenu 等模板部件全部找不到 → ForkWindow_Loaded NRE）。
		// override StyleKeyOverride 为 CustomWindow 后，CustomWindow 及其所有子类
		// （MainWindow/ReflogWindow...）均能命中 {x:Type ui:CustomWindow} ControlTheme。
		// 显式 Theme="{DynamicResource MainWindowStyle}" 优先级高于隐式主题，BasedOn 链不受影响。
		protected override global::System.Type StyleKeyOverride => typeof(CustomWindow);

		protected const string PartNameWindowHeader = "PART_WindowHeader";

		protected const string PartNameCloseButton = "PART_CloseButton";

		protected const string PartNameRestoreButton = "PART_RestoreButton";

		protected const string PartNameMinimizeButton = "PART_MinimizeButton";

		protected const string PartNameMaximizeButton = "PART_MaximizeButton";

		// TODO 迁移：字段类型从 AvaloniaProperty 收紧为 StyledProperty<T>（XAML 编译器要求 typed property，
		// 否则 TemplateBinding ui:CustomWindow.Xxx 报 "doesn't inherit from AvaloniaProperty<T>"）。
		public static readonly global::Avalonia.StyledProperty<double> HeaderHeightProperty;

		public static readonly global::Avalonia.StyledProperty<bool> ShowHeaderProperty;

		public static readonly global::Avalonia.StyledProperty<bool> HideMinimizeMaximizeButtonsProperty;

		public static readonly global::Avalonia.StyledProperty<bool> IsTitleVisibleProperty;

		public static readonly global::Avalonia.StyledProperty<global::Avalonia.Thickness> WindowResizeBorderThicknessProperty;

		private global::Avalonia.Controls.Control _templatePartWindowHeader;

		private Button _closeButton;

		private Button _minimizeButton;

		private Button _maximizeButton;

		private Button _restoreButton;

		private Thickness _tempWindowResizeBorderThickness;

		private Thickness _tempBorderThickness;

		private global::Avalonia.Controls.WindowState _tempWindowState;

		private bool _showHeader = true;

		private bool IsDesignMode => global::ForkPlus.DesignTimeHelper.IsInDesignMode();

		// ===== WPF 兼容成员（迁移期）=====
		// WPF Window.LocationChanged 事件 / OnLocationChanged 虚方法：Avalonia 对应 Window.PositionChanged。
		public event EventHandler LocationChanged;

		private bool _locationChangedHooked;

		protected override void OnOpened(EventArgs e)
		{
			base.OnOpened(e);
			if (!_locationChangedHooked)
			{
				_locationChangedHooked = true;
				PositionChanged += delegate
				{
					OnLocationChanged(EventArgs.Empty);
				};
			}
		}

		protected virtual void OnLocationChanged(EventArgs e)
		{
			LocationChanged?.Invoke(this, e);
		}

		// WPF Window.ResizeMode（Avalonia 12 无该属性，映射到 CanResize）。
		public global::ForkPlus.UI.WpfCompat.ResizeMode ResizeMode
		{
			get
			{
				return CanResize ? global::ForkPlus.UI.WpfCompat.ResizeMode.CanResize : global::ForkPlus.UI.WpfCompat.ResizeMode.NoResize;
			}
			set
			{
				CanResize = value != global::ForkPlus.UI.WpfCompat.ResizeMode.NoResize;
			}
		}

		public double HeaderHeight
		{
			get
			{
				return (double)GetValue(HeaderHeightProperty);
			}
			private set
			{
				SetValue(HeaderHeightProperty, value);
			}
		}

		public bool ShowHeader
		{
			get
			{
				return (bool)GetValue(ShowHeaderProperty);
			}
			set
			{
				SetValue(ShowHeaderProperty, value);
			}
		}

		public bool HideMinimizeMaximizeButtons
		{
			get
			{
				return (bool)GetValue(HideMinimizeMaximizeButtonsProperty);
			}
			set
			{
				SetValue(HideMinimizeMaximizeButtonsProperty, value);
			}
		}

		public bool IsTitleVisible
		{
			get
			{
				return (bool)GetValue(IsTitleVisibleProperty);
			}
			set
			{
				SetValue(IsTitleVisibleProperty, value);
			}
		}

		public Thickness WindowResizeBorderThickness
		{
			get
			{
				return (Thickness)GetValue(WindowResizeBorderThicknessProperty);
			}
			private set
			{
				SetValue(WindowResizeBorderThicknessProperty, value);
			}
		}

		private static Thickness MaximizedWindowResizeBorderThickness
		{
			get
			{
				Thickness windowResizeBorderThickness = WindowLocationStateExtensions.WindowResizeBorderThickness;
				if (WindowLocationStateExtensions.AutoHideEnabled())
				{
					return new Thickness(0.0 - windowResizeBorderThickness.Left, 0.0 - windowResizeBorderThickness.Top, 0.0 - windowResizeBorderThickness.Right, 0.0 - windowResizeBorderThickness.Bottom);
				}
				return windowResizeBorderThickness;
			}
		}

		static CustomWindow()
		{
			// TODO 迁移：WPF DependencyProperty.Register + PropertyMetadata → WpfPropertyCompat.Register（Avalonia StyledProperty）。
			HeaderHeightProperty = global::ForkPlus.UI.WpfCompat.WpfPropertyCompat.Register<CustomWindow, double>("HeaderHeight", 22.0);
			ShowHeaderProperty = global::ForkPlus.UI.WpfCompat.WpfPropertyCompat.Register<CustomWindow, bool>("ShowHeader", true, (owner, e) => OnShowHeaderChanged(owner, e));
			HideMinimizeMaximizeButtonsProperty = global::ForkPlus.UI.WpfCompat.WpfPropertyCompat.Register<CustomWindow, bool>("HideMinimizeMaximizeButtons", false);
			IsTitleVisibleProperty = global::ForkPlus.UI.WpfCompat.WpfPropertyCompat.Register<CustomWindow, bool>("IsTitleVisible", false);
			WindowResizeBorderThicknessProperty = global::ForkPlus.UI.WpfCompat.WpfPropertyCompat.Register<CustomWindow, global::Avalonia.Thickness>("WindowResizeBorderThickness", default(global::Avalonia.Thickness));
			// TODO 迁移：WPF DefaultStyleKeyProperty.OverrideMetadata 在 Avalonia 由 ControlTheme 接管，移除。
		}

		public CustomWindow()
		{
			// TODO 迁移（根因）：WPF WindowChrome 借 WM_NCCALCSIZE 自绘标题栏；Avalonia 12 对应
			// ExtendClientAreaToDecorationsHint——客户区延伸进装饰区，系统标题栏不再渲染
			// （Win32 走 DwmExtendFrameIntoClientArea + NCRP_ENABLED，原生边框/阴影/缩放保留）。
			// 修复"皮肤外还有系统原生窗口（含最小化/最大化/关闭）"。
			// TitleBarHeightHint=-1：按 OS 默认标题栏高度扩展（完全覆盖原生标题栏区域）。
			ExtendClientAreaToDecorationsHint = true;
			ExtendClientAreaTitleBarHeightHint = -1;
			// TODO 迁移：WPF SetResourceReference(StyleProperty, type) 隐式样式已由 Avalonia ControlTheme 接管，移除调用。;
			if (IsDesignMode)
			{
				_tempWindowResizeBorderThickness = new Thickness(6.0);
				WindowResizeBorderThickness = _tempWindowResizeBorderThickness;
				return;
			}
			WindowChrome windowChrome = new WindowChrome
			{
				CornerRadius = 0.0,
				GlassFrameThickness = new Thickness(0.0, 0.0, 0.0, 1.0),
				UseAeroCaptionButtons = false
			};
			Binding binding = new Binding("HeaderHeight")
			{
				Source = this
			};
			global::ForkPlus.UI.WpfCompat.BindingCompat.SetBinding(windowChrome, WindowChrome.CaptionHeightProperty, binding);
			WindowChrome.SetWindowChrome(this, windowChrome);
			_tempWindowResizeBorderThickness = WindowResizeBorderThickness;
			base.Loaded += Window_Loaded;
		}

		protected void OnContentRendered(EventArgs e)
		{
			if (base.SizeToContent == global::Avalonia.Controls.SizeToContent.WidthAndHeight)
			{
				InvalidateMeasure();
			}
		}

		// TODO 迁移（根因）：WPF Window.StateChanged 事件 / OnStateChanged 虚方法在 Avalonia 无直接
		// 对应（无 StateChanged 事件）。原 protected void OnStateChanged 无人调用（死代码），导致
		// 最大化/还原按钮可见性切换、最大化边框厚度调整全部失效。现通过 OnPropertyChanged
		// 监听 WindowStateProperty 转发，并改为 virtual 供子类（MainWindow 等）override。
		protected override void OnPropertyChanged(global::Avalonia.AvaloniaPropertyChangedEventArgs change)
		{
			base.OnPropertyChanged(change);
			if (change.Property == WindowStateProperty)
			{
				OnStateChanged(EventArgs.Empty);
			}
		}

		protected virtual void OnStateChanged(EventArgs e)
		{
			if (IsDesignMode)
			{
				return;
			}
			AdjustButtonsVisibilityToWindowState();
			if (base.WindowState == global::Avalonia.Controls.WindowState.Maximized)
			{
				WindowResizeBorderThickness = MaximizedWindowResizeBorderThickness;
				base.BorderThickness = default(Thickness);
			}
			else
			{
				WindowResizeBorderThickness = _tempWindowResizeBorderThickness;
				base.BorderThickness = _tempBorderThickness;
			}
		}

		protected override void OnApplyTemplate(global::Avalonia.Controls.Primitives.TemplateAppliedEventArgs e)
		{
			base.OnApplyTemplate(e);
			UnwireChromeEvents();
			_templatePartWindowHeader = this.GetTemplateChild("PART_WindowHeader") as global::Avalonia.Controls.Control;
			_closeButton = this.GetTemplateChild("PART_CloseButton") as Button;
			_minimizeButton = this.GetTemplateChild("PART_MinimizeButton") as Button;
			_maximizeButton = this.GetTemplateChild("PART_MaximizeButton") as Button;
			_restoreButton = this.GetTemplateChild("PART_RestoreButton") as Button;
			WireChromeEvents();
			AdjustButtonsVisibilityToWindowState();
		}

		// TODO 迁移（根因）：WPF 模板按钮用 Command="{x:Static SystemCommands.XxxWindowCommand}" +
		// Window.CommandBindings 路由；Avalonia 无 CommandBindings 路由（CommandRouter 只管键盘手势），
		// 模板按钮又不携带 Command → 最小化/最大化/还原/关闭全部失灵。改为代码后置直接挂 Click。
		private void WireChromeEvents()
		{
			if (_minimizeButton != null)
			{
				_minimizeButton.Click += MinimizeButton_Click;
			}
			if (_maximizeButton != null)
			{
				_maximizeButton.Click += MaximizeButton_Click;
			}
			if (_restoreButton != null)
			{
				_restoreButton.Click += RestoreButton_Click;
			}
			if (_closeButton != null)
			{
				_closeButton.Click += CloseButton_Click;
			}
			if (_templatePartWindowHeader != null)
			{
				_templatePartWindowHeader.PointerPressed += WindowHeader_PointerPressed;
				_templatePartWindowHeader.DoubleTapped += WindowHeader_DoubleTapped;
			}
		}

		/// <summary>模板可能重复应用（主题切换等），重挂前先解绑旧实例，防止重复触发。</summary>
		private void UnwireChromeEvents()
		{
			if (_minimizeButton != null)
			{
				_minimizeButton.Click -= MinimizeButton_Click;
			}
			if (_maximizeButton != null)
			{
				_maximizeButton.Click -= MaximizeButton_Click;
			}
			if (_restoreButton != null)
			{
				_restoreButton.Click -= RestoreButton_Click;
			}
			if (_closeButton != null)
			{
				_closeButton.Click -= CloseButton_Click;
			}
			if (_templatePartWindowHeader != null)
			{
				_templatePartWindowHeader.PointerPressed -= WindowHeader_PointerPressed;
				_templatePartWindowHeader.DoubleTapped -= WindowHeader_DoubleTapped;
			}
		}

		private void MinimizeButton_Click(object sender, RoutedEventArgs e)
		{
			base.WindowState = global::Avalonia.Controls.WindowState.Minimized;
		}

		private void MaximizeButton_Click(object sender, RoutedEventArgs e)
		{
			base.WindowState = global::Avalonia.Controls.WindowState.Maximized;
		}

		private void RestoreButton_Click(object sender, RoutedEventArgs e)
		{
			base.WindowState = global::Avalonia.Controls.WindowState.Normal;
		}

		private void CloseButton_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}

		// TODO 迁移（根因）：WPF WindowChrome.CaptionHeight 区域原生支持拖动/双击最大化；
		// Avalonia ExtendClientArea 后整个窗口都是客户区，需自实现标题栏交互。
		// Button/Menu 等控件会吃掉 PointerPressed（Handled），事件不再冒泡到标题栏，
		// 因此点按钮/菜单不会误触发拖动。
		private void WindowHeader_PointerPressed(object sender, global::Avalonia.Input.PointerPressedEventArgs e)
		{
			if (IsDesignMode)
			{
				return;
			}
			if (!CanResize)
			{
				return;
			}
			if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
			{
				try
				{
					BeginMoveDrag(e);
				}
				catch (global::System.InvalidOperationException)
				{
					// 窗口尚未显示等边缘状态：忽略，避免拖动破坏显示。
				}
			}
		}

		private void WindowHeader_DoubleTapped(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
		{
			if (IsDesignMode || !CanResize)
			{
				return;
			}
			// 双击按钮/菜单等交互元素时不切换最大化，仅双击空白标题栏生效。
			if (e.Source is global::Avalonia.Visual source && IsInteractiveChromeElement(source))
			{
				return;
			}
			if (base.WindowState == global::Avalonia.Controls.WindowState.Maximized)
			{
				base.WindowState = global::Avalonia.Controls.WindowState.Normal;
			}
			else
			{
				base.WindowState = global::Avalonia.Controls.WindowState.Maximized;
			}
		}

		/// <summary>判断可视元素（或其祖先）是否为交互控件（按钮/菜单/输入框），用于排除双击误判。</summary>
		private static bool IsInteractiveChromeElement(global::Avalonia.Visual visual)
		{
			for (global::Avalonia.Visual current = visual; current != null; current = current.GetVisualParent())
			{
				if (current is Button || current is global::Avalonia.Controls.MenuItem || current is global::Avalonia.Controls.Menu || current is global::Avalonia.Controls.TextBox)
				{
					return true;
				}
			}
			return false;
		}

		protected void OnSourceInitialized(EventArgs e)
		{
			if (IsDesignMode)
			{
				return;
			}
			// TODO 迁移：WPF HwndSource.AddHook(WM_NCCALCSIZE 等自绘 chrome 钩子)为 Win32 专用，
			// Avalonia 由 SystemDecorations/ExtendClientAreaChromeIntoTitleBar 替代，暂 no-op 保留 HwndSourceHook 方法。
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			if (IsDesignMode)
			{
				return;
			}
			// TODO 迁移（根因）：原 WPF SystemCommands 的 CommandBinding 路由在 Avalonia 无对应
			// （CommandRouter 只派发键盘手势，SystemCommands shim 不带手势，绑定永不触发），
			// 窗口按钮改为 OnApplyTemplate 里直接挂 Click（见 WireChromeEvents），此处删除死代码。
			_tempWindowState = base.WindowState;
			_tempBorderThickness = base.BorderThickness;
			if (base.WindowState == global::Avalonia.Controls.WindowState.Maximized)
			{
				base.BorderThickness = default(Thickness);
			}
			SwitchShowHeader(_showHeader);
		}

		private void AdjustButtonsVisibilityToWindowState()
		{
			if (_minimizeButton == null && _maximizeButton == null && _restoreButton == null)
			{
				return;
			}
			if (HideMinimizeMaximizeButtons)
			{
				_minimizeButton?.Collapse();
				_maximizeButton?.Collapse();
				_restoreButton?.Collapse();
				return;
			}
			switch (base.WindowState)
			{
			case global::Avalonia.Controls.WindowState.Normal:
				_maximizeButton?.Show();
				_restoreButton?.Collapse();
				break;
			case global::Avalonia.Controls.WindowState.Maximized:
				_maximizeButton?.Collapse();
				_restoreButton?.Show();
				break;
			}
			switch (ResizeMode)
			{
			case ResizeMode.NoResize:
				_minimizeButton?.Collapse();
				_maximizeButton?.Collapse();
				_restoreButton?.Collapse();
				break;
			case ResizeMode.CanMinimize:
				_maximizeButton?.Collapse();
				_restoreButton?.Collapse();
				break;
			}
		}

		private IntPtr HwndSourceHook(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam, ref bool handled)
		{
			switch (msg)
			{
			case 71:
				WindowResizeBorderThickness = ((base.WindowState == global::Avalonia.Controls.WindowState.Maximized) ? MaximizedWindowResizeBorderThickness : _tempWindowResizeBorderThickness);
				break;
			case 36:
				WindowLocationStateExtensions.GetMinMaxInfo(hwnd, lparam);
				WindowResizeBorderThickness = ((base.WindowState == global::Avalonia.Controls.WindowState.Maximized) ? MaximizedWindowResizeBorderThickness : _tempWindowResizeBorderThickness);
				handled = true;
				break;
			case 132:
				try
				{
					lparam.ToInt32();
				}
				catch (OverflowException)
				{
					handled = true;
				}
				break;
			}
			return IntPtr.Zero;
		}

		private static void OnShowHeaderChanged(global::Avalonia.AvaloniaObject d, global::Avalonia.AvaloniaPropertyChangedEventArgs e)
		{
			((CustomWindow)d).SwitchShowHeader((bool)e.NewValue);
		}

		private void SwitchShowHeader(bool showHeader)
		{
			if (_templatePartWindowHeader == null)
			{
				_showHeader = showHeader;
				return;
			}
			if (showHeader)
			{
				_templatePartWindowHeader.Show();
				return;
			}
			_templatePartWindowHeader.Collapse();
			HeaderHeight = 0.0;
		}
	}
}

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

namespace ForkPlus.UI
{
	[global::Avalonia.Controls.Metadata.TemplatePartAttribute(Name = "PART_MinimizeButton", Type = typeof(Button))]
	[global::Avalonia.Controls.Metadata.TemplatePartAttribute(Name = "PART_MaximizeButton", Type = typeof(Button))]
	[global::Avalonia.Controls.Metadata.TemplatePartAttribute(Name = "PART_RestoreButton", Type = typeof(Button))]
	[global::Avalonia.Controls.Metadata.TemplatePartAttribute(Name = "PART_CloseButton", Type = typeof(Button))]
	[global::Avalonia.Controls.Metadata.TemplatePartAttribute(Name = "PART_WindowHeader", Type = typeof(global::Avalonia.Controls.Control))]
	public class CustomWindow : Window
	{
		protected const string PartNameWindowHeader = "PART_WindowHeader";

		protected const string PartNameCloseButton = "PART_CloseButton";

		protected const string PartNameRestoreButton = "PART_RestoreButton";

		protected const string PartNameMinimizeButton = "PART_MinimizeButton";

		protected const string PartNameMaximizeButton = "PART_MaximizeButton";

		public static readonly global::Avalonia.AvaloniaProperty HeaderHeightProperty;

		public static readonly global::Avalonia.AvaloniaProperty ShowHeaderProperty;

		public static readonly global::Avalonia.AvaloniaProperty HideMinimizeMaximizeButtonsProperty;

		public static readonly global::Avalonia.AvaloniaProperty IsTitleVisibleProperty;

		public static readonly global::Avalonia.AvaloniaProperty WindowResizeBorderThicknessProperty;

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
			HeaderHeightProperty = global::Avalonia.AvaloniaProperty.Register("HeaderHeight", typeof(double), typeof(CustomWindow), new global::Avalonia.StyledPropertyMetadata(22.0));
			ShowHeaderProperty = global::Avalonia.AvaloniaProperty.Register("ShowHeader", typeof(bool), typeof(CustomWindow), new global::Avalonia.StyledPropertyMetadata(true, OnShowHeaderChanged));
			HideMinimizeMaximizeButtonsProperty = global::Avalonia.AvaloniaProperty.Register("HideMinimizeMaximizeButtons", typeof(bool), typeof(CustomWindow), new global::Avalonia.StyledPropertyMetadata(false));
			IsTitleVisibleProperty = global::Avalonia.AvaloniaProperty.Register("IsTitleVisible", typeof(bool), typeof(CustomWindow), new global::Avalonia.StyledPropertyMetadata(false));
			WindowResizeBorderThicknessProperty = global::Avalonia.AvaloniaProperty.Register("WindowResizeBorderThickness", typeof(Thickness), typeof(CustomWindow), new global::Avalonia.StyledPropertyMetadata(default(Thickness)));
			global::Avalonia.Controls.Control.DefaultStyleKeyProperty.OverrideMetadata(typeof(CustomWindow), new global::Avalonia.StyledPropertyMetadata(typeof(CustomWindow)));
		}

		public CustomWindow()
		{
			SetResourceReference(global::Avalonia.Controls.Control.StyleProperty, typeof(CustomWindow));
			if (IsDesignMode)
			{
				_tempWindowResizeBorderThickness = new Thickness(6.0);
				WindowResizeBorderThickness = _tempWindowResizeBorderThickness;
				return;
			}
			WindowChrome windowChrome = new WindowChrome
			{
				CornerRadius = default(CornerRadius),
				GlassFrameThickness = new Thickness(0.0, 0.0, 0.0, 1.0),
				UseAeroCaptionButtons = false
			};
			Binding binding = new Binding("HeaderHeight")
			{
				Source = this
			};
			BindingOperations.SetBinding(windowChrome, WindowChrome.CaptionHeightProperty, binding);
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

		protected void OnStateChanged(EventArgs e)
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
			_templatePartWindowHeader = GetTemplateChild("PART_WindowHeader") as global::Avalonia.Controls.Control;
			_closeButton = GetTemplateChild("PART_CloseButton") as Button;
			_minimizeButton = GetTemplateChild("PART_MinimizeButton") as Button;
			_maximizeButton = GetTemplateChild("PART_MaximizeButton") as Button;
			_restoreButton = GetTemplateChild("PART_RestoreButton") as Button;
			AdjustButtonsVisibilityToWindowState();
		}

		protected void OnSourceInitialized(EventArgs e)
		{
			if (IsDesignMode)
			{
				return;
			}
			HwndSource.FromHwnd(new WindowInteropHelper(this).EnsureHandle()).AddHook(HwndSourceHook);
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			if (IsDesignMode)
			{
				return;
			}
			this.AddCommandBinding(new CommandBinding(SystemCommands.MinimizeWindowCommand, delegate
			{
				base.WindowState = global::Avalonia.Controls.WindowState.Minimized;
			}));
			this.AddCommandBinding(new CommandBinding(SystemCommands.MaximizeWindowCommand, delegate
			{
				base.WindowState = global::Avalonia.Controls.WindowState.Maximized;
			}));
			this.AddCommandBinding(new CommandBinding(SystemCommands.RestoreWindowCommand, delegate
			{
				base.WindowState = global::Avalonia.Controls.WindowState.Normal;
			}));
			this.AddCommandBinding(new CommandBinding(SystemCommands.CloseWindowCommand, delegate
			{
				Close();
			}));
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
			switch (base.ResizeMode)
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

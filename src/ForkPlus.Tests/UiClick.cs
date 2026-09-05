// E2E 测试基建（阶段0，2026-09-05）：控件查找 + 模拟交互。
// 铁律：定位用控件树遍历（类型 + 名字/内容），绝不依赖截图识别。
// 点击用 RaiseEvent 路由事件，与 headless 事件管线一致
//（现有 30+ 测试实证该方式有效：UiSmokeHeadlessTests / DiffSelectionFloatingButtonsTests /
//  TitleBarDragSnapTests / DoubleTapProbeTests 的构造器模式全部收编于此）。
using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace ForkPlus.Tests
{
	internal static class UiClick
	{
		// ============================ 查找 ============================

		/// <summary>在 root 可视子树里按名字找第一个 T。</summary>
		public static T Find<T>(Visual root, string name) where T : class
		{
			T found = TryFind<T>(root, name);
			return found ?? throw new InvalidOperationException("找不到控件 " + typeof(T).Name + " name=" + name);
		}

		public static T TryFind<T>(Visual root, string name) where T : class
		{
			foreach (Visual v in root.GetVisualDescendants())
			{
				if (v is StyledElement se && se.Name == name && v is T t)
				{
					return t;
				}
			}
			return null;
		}

		/// <summary>按类型找第一个。</summary>
		public static T Find<T>(Visual root) where T : Visual
		{
			foreach (Visual v in root.GetVisualDescendants())
			{
				if (v is T t)
				{
					return t;
				}
			}
			throw new InvalidOperationException("找不到控件类型 " + typeof(T).Name);
		}

		/// <summary>找所有指定类型的后代（含模板内部）。</summary>
		public static List<T> FindAll<T>(Visual root) where T : Visual
		{
			var list = new List<T>();
			foreach (Visual v in root.GetVisualDescendants())
			{
				if (v is T t)
				{
					list.Add(t);
				}
			}
			return list;
		}

		/// <summary>找第 N 个指定类型后代（不依赖名字——模板内部控件常无名）。</summary>
		public static T FindAt<T>(Visual root, int index) where T : Visual
		{
			var list = FindAll<T>(root);
			return index < list.Count ? list[index] : null;
		}

		/// <summary>按 Content 文本找按钮（对模板按钮/本地化按钮最实用）。</summary>
		public static Button FindButtonByText(Visual root, string text)
		{
			foreach (Visual v in root.GetVisualDescendants())
			{
				if (v is Button b && ContentText(b) == text)
				{
					return b;
				}
			}
			throw new InvalidOperationException("找不到按钮 text=" + text);
		}

		public static string ContentText(ContentControl c)
		{
			return c.Content as string ?? (c.Content as TextBlock)?.Text ?? string.Empty;
		}

		// ============================ 交互 ============================

		/// <summary>点击按钮（RaiseEvent 路由事件，走完整 handler 管线）。</summary>
		public static void Click(Button button)
		{
			button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
			Dispatcher.UIThread.RunJobs();
		}

		/// <summary>命中测试意义上的"按下并释放"（列表行选中、可点区域）。</summary>
		public static void Press(InputElement control, Window window, Point position)
		{
			var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
			var properties = new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed);
			var pressed = new PointerPressedEventArgs(control, pointer, window, position, (ulong)Environment.TickCount64, properties, KeyModifiers.None);
			control.RaiseEvent(pressed);
			var released = new PointerReleasedEventArgs(control, pointer, window, position, (ulong)Environment.TickCount64, properties, KeyModifiers.None, MouseButton.Left);
			control.RaiseEvent(released);
			Dispatcher.UIThread.RunJobs();
		}

		/// <summary>单击（Tapped 手势）。</summary>
		public static void Tap(InputElement control, Window window, Point position)
		{
			var pointerArgs = new PointerEventArgs(
				InputElement.PointerMovedEvent, control,
				new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true),
				window, position, (ulong)Environment.TickCount64,
				new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.Other),
				KeyModifiers.None);
			control.RaiseEvent(new TappedEventArgs(InputElement.TappedEvent, pointerArgs) { Source = control });
			Dispatcher.UIThread.RunJobs();
		}

		/// <summary>双击（DoubleTapped 手势）。</summary>
		public static void DoubleTap(InputElement control, Window window, Point position)
		{
			var pointerArgs = new PointerEventArgs(
				InputElement.PointerMovedEvent, control,
				new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true),
				window, position, (ulong)Environment.TickCount64,
				new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.Other),
				KeyModifiers.None);
			control.RaiseEvent(new TappedEventArgs(InputElement.DoubleTappedEvent, pointerArgs) { Source = control });
			Dispatcher.UIThread.RunJobs();
		}

		/// <summary>设置复选状态。Avalonia 在 IsChecked 属性变化时自动路由 Checked/Unchecked 事件，
		/// 直接赋值即可触发完整管线（勿手动 RaiseEvent 造成双触发）。</summary>
		public static void Toggle(CheckBox checkBox, bool isChecked)
		{
			checkBox.IsChecked = isChecked;
			Dispatcher.UIThread.RunJobs();
		}

		/// <summary>选中下拉项。设置 SelectedIndex 自动路由 SelectionChanged。</summary>
		public static void Select(ComboBox comboBox, int index)
		{
			comboBox.SelectedIndex = index;
			Dispatcher.UIThread.RunJobs();
		}

		/// <summary>展开/收起 TreeViewItem（含模板PART）。</summary>
		public static void ToggleExpand(TreeViewItem item, bool expanded)
		{
			item.IsExpanded = expanded;
			Dispatcher.UIThread.RunJobs();
		}
	}
}

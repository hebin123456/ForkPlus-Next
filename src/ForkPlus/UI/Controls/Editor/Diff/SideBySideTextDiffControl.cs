using System;
using ForkPlus.UI.WpfCompat;
using Avalonia.Controls;
using ForkPlus.Git.Diff;
using ForkPlus.Git.Diff.Presentation;
using ForkPlus.UI.Helpers;
using Avalonia.Threading;

namespace ForkPlus.UI.Controls.Editor.Diff
{
	public class SideBySideTextDiffControl : Grid, ITextDiffControl, DiffControlContainer.IFileDiffControlSubControl
	{
		private readonly DiffCodeEditor _leftDiffCodeEditor;

		private readonly DiffCodeEditor _rightDiffCodeEditor;

		// 修复（2026-09-05，"点击横向滚动条界面弹动"）：
		// 原逻辑垂直/水平共用一个时间戳防抖，横向滚动触发 ScrollOffsetChanged 时
		// 会读取当前 Y 偏移并同步到对侧；若对侧的 ScrollOffsetChanged 回调在
		// 100ms 之外又反向同步，可能造成滚动位置意外变化甚至震荡。
		// 修复：1) 垂直/水平分别防抖；2) 同步前检查差值，接近则跳过，避免
		// 无意义的 ScrollOffsetChanged 触发联动循环。
		private DateTime _lastVerticalScrollTime;
		private DateTime _lastHorizontalScrollTime;
		private DiffCodeEditor _lastVerticalEditor;
		private DiffCodeEditor _lastHorizontalEditor;
		private double _lastSyncedVerticalOffset = double.NaN;
		private double _lastSyncedHorizontalOffset = double.NaN;

		[Null]
		public CodeEditorScrollPositionCache PositionCache { get; set; }

		[Null]
		public ForkPlus.Git.Diff.Diff Diff { get; private set; }

		public int TabWidth { get; private set; }

		public bool EntireFile { get; private set; }

		public DiffLocation Location { get; private set; }

		public global::Avalonia.Controls.Primitives.ScrollBarVisibility VerticalScrollBarVisibility
		{
			get
			{
				return _rightDiffCodeEditor.VerticalScrollBarVisibility;
			}
			set
			{
				_rightDiffCodeEditor.VerticalScrollBarVisibility = value;
			}
		}

		public event ContextMenuEventHandler EditorContextMenuOpening
		{
			add
			{
				global::ForkPlus.UI.WpfCompat.ContextMenuCompat.AddContextMenuOpeningHandler(_leftDiffCodeEditor,(s, e) => value?.Invoke(s, e));
				global::ForkPlus.UI.WpfCompat.ContextMenuCompat.AddContextMenuOpeningHandler(_rightDiffCodeEditor,(s, e) => value?.Invoke(s, e));
			}
			remove
			{
				global::ForkPlus.UI.WpfCompat.ContextMenuCompat.RemoveContextMenuOpeningHandler(_leftDiffCodeEditor,(s, e) => value?.Invoke(s, e));
				global::ForkPlus.UI.WpfCompat.ContextMenuCompat.RemoveContextMenuOpeningHandler(_rightDiffCodeEditor,(s, e) => value?.Invoke(s, e));
			}
		}

		public SideBySideTextDiffControl()
		{
			_leftDiffCodeEditor = new DiffCodeEditor(DiffViewMode.SideBySideOld);
			_rightDiffCodeEditor = new DiffCodeEditor(DiffViewMode.SideBySideNew);
			_leftDiffCodeEditor.ContextMenu = new ContextMenu();
			_rightDiffCodeEditor.ContextMenu = new ContextMenu();
			_leftDiffCodeEditor.HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch;
			_leftDiffCodeEditor.VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Stretch;
			_rightDiffCodeEditor.HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch;
			_rightDiffCodeEditor.VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Stretch;
			global::ForkPlus.UI.WpfCompat.ContextMenuCompat.AddContextMenuClosingHandler(_leftDiffCodeEditor,delegate
			{
				_leftDiffCodeEditor.ContextMenu.Items.Clear();
			});
			global::ForkPlus.UI.WpfCompat.ContextMenuCompat.AddContextMenuClosingHandler(_rightDiffCodeEditor,delegate
			{
				_rightDiffCodeEditor.ContextMenu.Items.Clear();
			});
			base.ColumnDefinitions.Add(new ColumnDefinition());
			base.ColumnDefinitions.Add(new ColumnDefinition());
			base.Children.Add(_leftDiffCodeEditor);
			base.Children.Add(_rightDiffCodeEditor);
			_leftDiffCodeEditor.SetValue(Grid.ColumnProperty, 0);
			_rightDiffCodeEditor.SetValue(Grid.ColumnProperty, 1);
			_leftDiffCodeEditor.VerticalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden;
			_leftDiffCodeEditor.TextArea.TextView.ScrollOffsetChanged += delegate
			{
				OnScrollOffsetChanged(_leftDiffCodeEditor);
			};
			_rightDiffCodeEditor.TextArea.TextView.ScrollOffsetChanged += delegate
			{
				OnScrollOffsetChanged(_rightDiffCodeEditor);
			};
		}

		public void ControlWillBeRemovedFromFileDiffControl()
		{
			PositionCache?.SaveScrollPosition(_leftDiffCodeEditor, _rightDiffCodeEditor);
		}

		public void SetDiff(ForkPlus.Git.Diff.Diff diff, int tabWidth, bool entireFile, DiffLocation location)
		{
			Diff = diff;
			TabWidth = tabWidth;
			EntireFile = entireFile;
			Location = location;
			PositionCache?.SaveScrollPosition(_leftDiffCodeEditor, _rightDiffCodeEditor);
			VisualPatch.CreateSideBySideVisualPatch(Diff, EntireFile, Location, out var old, out var @new);
			_leftDiffCodeEditor.Options.IndentationSize = tabWidth;
			_leftDiffCodeEditor.VisualPatch = old;
			_rightDiffCodeEditor.Options.IndentationSize = tabWidth;
			_rightDiffCodeEditor.VisualPatch = @new;
			base.Dispatcher.Post(delegate
			{
				PositionCache?.RestoreScrollPosition(_leftDiffCodeEditor, _rightDiffCodeEditor);
			});
		}

		public void RefreshDiffFont(double codeEditorFontSize)
		{
			_leftDiffCodeEditor.FontSize = codeEditorFontSize;
			_rightDiffCodeEditor.FontSize = codeEditorFontSize;
		}

		public void RefreshDiffWordWrap(bool diffWordWrap)
		{
			_leftDiffCodeEditor.WordWrap = false;
			_rightDiffCodeEditor.WordWrap = false;
		}

		public void RefreshDiffShowHiddenSymbols(bool diffShowHiddenSymbols)
		{
			_leftDiffCodeEditor.Options.ShowSpaces = diffShowHiddenSymbols;
			_rightDiffCodeEditor.Options.ShowSpaces = diffShowHiddenSymbols;
			_leftDiffCodeEditor.Options.ShowTabs = diffShowHiddenSymbols;
			_rightDiffCodeEditor.Options.ShowTabs = diffShowHiddenSymbols;
		}

		public void ScrollToPreviousCustomHunk()
		{
			_rightDiffCodeEditor.ScrollToPreviousCustomHunk();
		}

		public void ScrollToNextCustomHunk()
		{
			_rightDiffCodeEditor.ScrollToNextCustomHunk();
		}

		private void OnScrollOffsetChanged(DiffCodeEditor editor)
		{
			double verticalOffset = editor.TextArea.TextView.ScrollOffset.Y;
			double horizontalOffset = editor.TextArea.TextView.ScrollOffset.X;

			// ── 垂直滚动同步 ──
			if (editor.IsVerticalOffsetWithinDocumentArea(verticalOffset))
			{
				// 防抖：同一方向 100ms 内来自对侧的联动回调直接忽略
				if (!(DateTime.Now - _lastVerticalScrollTime < TimeSpan.FromMilliseconds(100.0)
					&& editor != _lastVerticalEditor))
				{
					// 同步前检查：如果目标编辑器当前偏移已经接近目标值，则跳过，
					// 避免无意义的 ScrollTo 调用触发 ScrollOffsetChanged 形成循环
					const double vTolerance = 0.5;
					bool synced = false;
					if (editor != _leftDiffCodeEditor
						&& _leftDiffCodeEditor.IsVerticalOffsetWithinDocumentArea(verticalOffset)
						&& Math.Abs(_leftDiffCodeEditor.TextArea.TextView.ScrollOffset.Y - verticalOffset) > vTolerance)
					{
						_leftDiffCodeEditor.ScrollToVerticalOffsetCompat(verticalOffset);
						synced = true;
					}
					if (editor != _rightDiffCodeEditor
						&& _rightDiffCodeEditor.IsVerticalOffsetWithinDocumentArea(verticalOffset)
						&& Math.Abs(_rightDiffCodeEditor.TextArea.TextView.ScrollOffset.Y - verticalOffset) > vTolerance)
					{
						_rightDiffCodeEditor.ScrollToVerticalOffsetCompat(verticalOffset);
						synced = true;
					}
					if (synced)
					{
						_lastVerticalScrollTime = DateTime.Now;
						_lastVerticalEditor = editor;
					}
				}
			}

			// ── 水平滚动同步 ──
			if (editor.IsHorizontalOffsetWithinDocumentArea(horizontalOffset))
			{
				if (!(DateTime.Now - _lastHorizontalScrollTime < TimeSpan.FromMilliseconds(100.0)
					&& editor != _lastHorizontalEditor))
				{
					const double hTolerance = 0.5;
					bool synced = false;
					if (editor != _leftDiffCodeEditor
						&& _leftDiffCodeEditor.IsHorizontalOffsetWithinDocumentArea(horizontalOffset)
						&& Math.Abs(_leftDiffCodeEditor.TextArea.TextView.ScrollOffset.X - horizontalOffset) > hTolerance)
					{
						_leftDiffCodeEditor.ScrollToHorizontalOffsetCompat(horizontalOffset);
						synced = true;
					}
					if (editor != _rightDiffCodeEditor
						&& _rightDiffCodeEditor.IsHorizontalOffsetWithinDocumentArea(horizontalOffset)
						&& Math.Abs(_rightDiffCodeEditor.TextArea.TextView.ScrollOffset.X - horizontalOffset) > hTolerance)
					{
						_rightDiffCodeEditor.ScrollToHorizontalOffsetCompat(horizontalOffset);
						synced = true;
					}
					if (synced)
					{
						_lastHorizontalScrollTime = DateTime.Now;
						_lastHorizontalEditor = editor;
					}
				}
			}
		}
	}
}

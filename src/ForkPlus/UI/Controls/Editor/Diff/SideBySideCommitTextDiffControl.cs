using System;
using ForkPlus.UI.WpfCompat;
using Avalonia.Controls;
using ForkPlus.Git.Diff;
using ForkPlus.Git.Diff.Presentation;
using ForkPlus.UI.Helpers;
using Avalonia.Threading;

namespace ForkPlus.UI.Controls.Editor.Diff
{
	public class SideBySideCommitTextDiffControl : Grid, ICommitTextDiffControl, ITextDiffControl, DiffControlContainer.IFileDiffControlSubControl
	{
		private CommitCodeEditor _leftDiffCodeEditor;

		private CommitCodeEditor _rightDiffCodeEditor;

		// 修复（2026-09-05，"点击横向滚动条界面弹动"）：
		// 垂直/水平滚动分别防抖；同步前检查差值，避免联动循环。
		private DateTime _lastVerticalScrollTime;
		private DateTime _lastHorizontalScrollTime;
		private DiffCodeEditor _lastVerticalEditor;
		private DiffCodeEditor _lastHorizontalEditor;

		[Null]
		public CodeEditorScrollPositionCache PositionCache { get; set; }

		[Null]
		public ForkPlus.Git.Diff.Diff Diff { get; private set; }

		public int TabWidth { get; private set; }

		public bool EntireFile { get; private set; }

		public DiffLocation Location { get; private set; }

		public bool IsStaged
		{
			get
			{
				return _rightDiffCodeEditor.IsStaged;
			}
			set
			{
				_leftDiffCodeEditor.IsStaged = value;
				_rightDiffCodeEditor.IsStaged = value;
			}
		}

		public bool IsNewOrUntracked
		{
			get
			{
				return _rightDiffCodeEditor.IsNewOrUntracked;
			}
			set
			{
				_leftDiffCodeEditor.IsNewOrUntracked = value;
				_rightDiffCodeEditor.IsNewOrUntracked = value;
			}
		}

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

		public event EventHandler<CommitCodeEditor> ToggleStage
		{
			add
			{
				_leftDiffCodeEditor.ToggleStage += value;
				_rightDiffCodeEditor.ToggleStage += value;
			}
			remove
			{
				_leftDiffCodeEditor.ToggleStage -= value;
				_rightDiffCodeEditor.ToggleStage -= value;
			}
		}

		public event EventHandler<CommitCodeEditor> Stage
		{
			add
			{
				_leftDiffCodeEditor.Stage += value;
				_rightDiffCodeEditor.Stage += value;
			}
			remove
			{
				_leftDiffCodeEditor.Stage -= value;
				_rightDiffCodeEditor.Stage -= value;
			}
		}

		public event EventHandler<CommitCodeEditor> Unstage
		{
			add
			{
				_leftDiffCodeEditor.UnStage += value;
				_rightDiffCodeEditor.UnStage += value;
			}
			remove
			{
				_leftDiffCodeEditor.UnStage -= value;
				_rightDiffCodeEditor.UnStage -= value;
			}
		}

		public event EventHandler<CommitCodeEditor> Discard
		{
			add
			{
				_leftDiffCodeEditor.Discard += value;
				_rightDiffCodeEditor.Discard += value;
			}
			remove
			{
				_leftDiffCodeEditor.Discard -= value;
				_rightDiffCodeEditor.Discard -= value;
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

		public SideBySideCommitTextDiffControl()
		{
			_leftDiffCodeEditor = new CommitCodeEditor(DiffViewMode.SideBySideOld);
			_rightDiffCodeEditor = new CommitCodeEditor(DiffViewMode.SideBySideNew);
			_leftDiffCodeEditor.Sync(_rightDiffCodeEditor);
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

		public void SetDiff([Null] ForkPlus.Git.Diff.Diff diff, int tabWidth, bool entireFile, DiffLocation location)
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
				if (!(DateTime.Now - _lastVerticalScrollTime < TimeSpan.FromMilliseconds(100.0)
					&& editor != _lastVerticalEditor))
				{
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

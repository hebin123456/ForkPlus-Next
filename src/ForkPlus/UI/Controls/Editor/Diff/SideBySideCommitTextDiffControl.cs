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

		private DateTime _lastLastScrollTime;

		private DiffCodeEditor _lastUpdatedEditor;

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
				global::ForkPlus.UI.WpfCompat.ContextMenuCompat.AddContextMenuOpeningHandler(_leftDiffCodeEditor,value);
				global::ForkPlus.UI.WpfCompat.ContextMenuCompat.AddContextMenuOpeningHandler(_rightDiffCodeEditor,value);
			}
			remove
			{
				global::ForkPlus.UI.WpfCompat.ContextMenuCompat.RemoveContextMenuOpeningHandler(_leftDiffCodeEditor,value);
				global::ForkPlus.UI.WpfCompat.ContextMenuCompat.RemoveContextMenuOpeningHandler(_rightDiffCodeEditor,value);
			}
		}

		public SideBySideCommitTextDiffControl()
		{
			_leftDiffCodeEditor = new CommitCodeEditor(DiffViewMode.SideBySideOld);
			_rightDiffCodeEditor = new CommitCodeEditor(DiffViewMode.SideBySideNew);
			_leftDiffCodeEditor.Sync(_rightDiffCodeEditor);
			_leftDiffCodeEditor.ContextMenu = new ContextMenu();
			_rightDiffCodeEditor.ContextMenu = new ContextMenu();
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
			if (DateTime.Now - _lastLastScrollTime < TimeSpan.FromMilliseconds(100.0) && editor != _lastUpdatedEditor)
			{
				return;
			}
			double verticalOffset = editor.TextArea.TextView.ScrollOffset.Y;
			double horizontalOffset = editor.TextArea.TextView.ScrollOffset.X;
			if (editor.IsVerticalOffsetWithinDocumentArea(verticalOffset))
			{
				if (editor != _leftDiffCodeEditor)
				{
					ScrollToVerticalOffset(_leftDiffCodeEditor, verticalOffset);
				}
				if (editor != _rightDiffCodeEditor)
				{
					ScrollToVerticalOffset(_rightDiffCodeEditor, verticalOffset);
				}
			}
			if (editor.IsHorizontalOffsetWithinDocumentArea(horizontalOffset))
			{
				if (editor != _leftDiffCodeEditor)
				{
					ScrollToHorizontalOffset(_leftDiffCodeEditor, horizontalOffset);
				}
				if (editor != _rightDiffCodeEditor)
				{
					ScrollToHorizontalOffset(_rightDiffCodeEditor, horizontalOffset);
				}
			}
			_lastLastScrollTime = DateTime.Now;
			_lastUpdatedEditor = editor;
		}

		private static void ScrollToVerticalOffset(DiffCodeEditor editor, double offset)
		{
			if (editor.IsVerticalOffsetWithinDocumentArea(offset))
			{
				editor.ScrollToVerticalOffsetCompat(offset);
			}
		}

		private static void ScrollToHorizontalOffset(DiffCodeEditor editor, double offset)
		{
			if (editor.IsHorizontalOffsetWithinDocumentArea(offset))
			{
				editor.ScrollToHorizontalOffsetCompat(offset);
			}
		}
	}
}

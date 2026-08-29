using System;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Controls
{
	public class SelectableTextBlock : TextBlock
	{
		private class TextEditorWrapper
		{
			private static readonly Type TextEditorType = Type.GetType("System.Windows.Documents.TextEditor, PresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35");

			private static readonly PropertyInfo IsReadOnlyProp = TextEditorType.GetProperty("IsReadOnly", BindingFlags.Instance | BindingFlags.NonPublic);

			private static readonly PropertyInfo TextViewProp = TextEditorType.GetProperty("TextView", BindingFlags.Instance | BindingFlags.NonPublic);

			private static readonly MethodInfo RegisterMethod = TextEditorType.GetMethod("RegisterCommandHandlers", BindingFlags.Static | BindingFlags.NonPublic, null, new Type[4]
			{
				typeof(Type),
				typeof(bool),
				typeof(bool),
				typeof(bool)
			}, null);

			private static readonly Type TextContainerType = Type.GetType("System.Windows.Documents.ITextContainer, PresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35");

			private static readonly PropertyInfo TextContainerTextViewProp = TextContainerType.GetProperty("TextView");

			private static readonly PropertyInfo TextContainerProp = typeof(TextBlock).GetProperty("TextContainer", BindingFlags.Instance | BindingFlags.NonPublic);

			private readonly object _editor;

			public static void RegisterCommandHandlers(Type controlType, bool acceptsRichContent, bool readOnly, bool registerEventListeners)
			{
				RegisterMethod.Invoke(null, new object[4] { controlType, acceptsRichContent, readOnly, registerEventListeners });
			}

			public static TextEditorWrapper CreateFor(TextBlock tb)
			{
				object value = TextContainerProp.GetValue(tb);
				TextEditorWrapper textEditorWrapper = new TextEditorWrapper(value, tb, isUndoEnabled: false);
				IsReadOnlyProp.SetValue(textEditorWrapper._editor, true);
				TextViewProp.SetValue(textEditorWrapper._editor, TextContainerTextViewProp.GetValue(value));
				return textEditorWrapper;
			}

			public TextEditorWrapper(object textContainer, global::Avalonia.Controls.Control uiScope, bool isUndoEnabled)
			{
				_editor = Activator.CreateInstance(TextEditorType, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.CreateInstance, null, new object[3] { textContainer, uiScope, isUndoEnabled }, null);
			}
		}

		private readonly TextEditorWrapper _editor;

		static SelectableTextBlock()
		{
			// TODO 迁移：WPF FocusableProperty.OverrideMetadata(default true) → 构造函数 Focusable = true；
			// FocusVisualStyle 在 Avalonia 无对应（焦点视觉由主题伪类承担），移除。
			TextEditorWrapper.RegisterCommandHandlers(typeof(SelectableTextBlock), acceptsRichContent: true, readOnly: true, registerEventListeners: true);
		}

		public SelectableTextBlock()
		{
			// TODO 迁移：WPF 属性元数据默认值迁移至此（Avalonia OverrideMetadata 为私有 API）。
			Focusable = true;
			_editor = TextEditorWrapper.CreateFor(this);
		}
	}
}

using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Threading;
using ForkPlus.Git;
using ForkPlus.UI.UserControls;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Interactivity;

namespace ForkPlus.UI.Controls
{
	public class CommandHyperlink : global::Avalonia.Controls.HyperlinkButton
	{
		private readonly RepositoryUserControl _repositoryUserControl;

		private readonly Sha _sha;

		private readonly DispatcherTimer _showPopupTimer = new DispatcherTimer();

		private readonly DispatcherTimer _closePopupTimer = new DispatcherTimer();

		[Null]
		private Popup _popup;

		private readonly Action _action;

		public CommandHyperlink(RepositoryUserControl repositoryUserControl, Sha sha, string text, Action action)
			: base(new Run(text))
		{
			_action = action;
			_repositoryUserControl = repositoryUserControl;
			_sha = sha;
			_showPopupTimer.Interval = TimeSpan.FromMilliseconds(500.0);
			_closePopupTimer.Interval = TimeSpan.FromMilliseconds(100.0);
			_showPopupTimer.Tick += _showPopupTimer_Tick;
			_closePopupTimer.Tick += _closePopupTimer_Tick;
			base.Style = Application.Current.TryFindResource("BugtrackerHyperlinkStyle") as Style;
			base.Click += CommandHyperlink_Click;
			base.PointerEntered += delegate(object s, global::Avalonia.Input.PointerEventArgs e)
			{
				e.Handled = true;
				_closePopupTimer.Stop();
				_showPopupTimer.Start();
			};
			base.PointerExited += delegate(object s, global::Avalonia.Input.PointerEventArgs e)
			{
				e.Handled = true;
				_showPopupTimer.Stop();
				_closePopupTimer.Start();
			};
		}

		private void CommandHyperlink_Click(object sender, RoutedEventArgs e)
		{
			ClosePopup(hardClose: true);
			_showPopupTimer.Stop();
			_action();
		}

		private void _showPopupTimer_Tick(object sender, EventArgs e)
		{
			ShowPopup();
			_showPopupTimer.Stop();
		}

		private void _closePopupTimer_Tick(object sender, EventArgs e)
		{
			ClosePopup();
			_closePopupTimer.Stop();
		}

		private void ShowPopup()
		{
			if (_popup == null || !_popup.IsOpen)
			{
				_popup = CreatePopup();
				if (_popup != null)
				{
					_popup.IsOpen = true;
				}
			}
		}

		private void ClosePopup(bool hardClose = false)
		{
			if (_popup != null && _popup.IsOpen && (!_popup.IsMouseOver || hardClose))
			{
				_popup.IsOpen = false;
				VisualTreeAttachmentHelper.TrySetPopupChild(_popup, null, GetType().Name + ".Popup");
				_popup = null;
			}
		}

		[Null]
		private Popup CreatePopup()
		{
			if (!(base.Parent is TextBlock placementTarget))
			{
				return null;
			}
			Popup obj = new Popup
			{
				HorizontalOffset = -10.0,
				VerticalOffset = -4.0,
				StaysOpen = true,
				AllowsTransparency = true,
				PopupAnimation = PopupAnimation.Fade,
				PlacementTarget = placementTarget
			};
			Rect placementRectangle = Rect.Union(base.ElementStart.GetCharacterRect(LogicalDirection.Forward), base.ElementEnd.GetCharacterRect(LogicalDirection.Backward));
			placementRectangle.X += placementRectangle.Width / 2.0;
			obj.PlacementRectangle = placementRectangle;
			TooltipRevisionDetailsUserControl tooltipRevisionDetailsUserControl = new TooltipRevisionDetailsUserControl(_repositoryUserControl, _sha);
			tooltipRevisionDetailsUserControl.ShowRevisionInSeparateWindowButtonClicked = (EventHandler)Delegate.Combine(tooltipRevisionDetailsUserControl.ShowRevisionInSeparateWindowButtonClicked, (EventHandler)delegate
			{
				ClosePopup(hardClose: true);
			});
			VisualTreeAttachmentHelper.TrySetPopupChild(obj, tooltipRevisionDetailsUserControl, GetType().Name + ".Popup");
			obj.PointerExited += delegate
			{
				_closePopupTimer.Start();
			};
			return obj;
		}
	}
}

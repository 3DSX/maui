using Android.Widget;
using AndroidX.Core.View;
using ALayoutDirection = Android.Views.LayoutDirection;
using ATextDirection = Android.Views.TextDirection;
using AView = Android.Views.View;

namespace Microsoft.Maui
{
	public static class ViewExtensions
	{
		const int DefaultAutomationTagId = -1;
		public static int AutomationTagId { get; set; } = DefaultAutomationTagId;

		public static void UpdateIsEnabled(this AView nativeView, IView view)
		{
			if (nativeView != null)
				nativeView.Enabled = view.IsEnabled;
		}

		public static void UpdateBackgroundColor(this AView nativeView, IView view)
		{
			var backgroundColor = view.BackgroundColor;
			if (backgroundColor != null)
				nativeView?.SetBackgroundColor(backgroundColor.ToNative());
		}

		public static void UpdateFlowDirection(this AView nativeView, IView view)
		{
			if (view.FlowDirection.IsRightToLeft())
			{
				nativeView.LayoutDirection = ALayoutDirection.Rtl;

				if (nativeView is TextView textView)
					textView.TextDirection = ATextDirection.Rtl;
			}
			else if (view.FlowDirection.IsLeftToRight())
			{
				nativeView.LayoutDirection = ALayoutDirection.Ltr;

				if (nativeView is TextView textView)
					textView.TextDirection = ATextDirection.Ltr;
			}
		}

		public static bool GetClipToOutline(this AView view)
		{
			return view.ClipToOutline;
		}

		public static void SetClipToOutline(this AView view, bool value)
		{
			view.ClipToOutline = value;
		}

		public static void UpdateAutomationId(this AView nativeView, IView view)
		{
			if (AutomationTagId == DefaultAutomationTagId)
			{
				AutomationTagId = Microsoft.Maui.Resource.Id.automation_tag_id;
			}

			nativeView.SetTag(AutomationTagId, view.AutomationId);
		}

		public static void UpdateSemantics(this AView nativeView, IView view)
		{
			var semantics = view.Semantics;
			if (semantics == null)
				return;

			nativeView.ContentDescription = semantics.Description;
			ViewCompat.SetAccessibilityHeading(nativeView, semantics.IsHeading);
		}

		public static void InvalidateMeasure(this AView nativeView, IView view)
		{
			nativeView.RequestLayout();
		}

		public static void UpdateWidth(this AView nativeView, IView view)
		{
			// GetDesiredSize will take the specified Width into account during the layout
			if (!nativeView.IsInLayout)
			{
				nativeView.RequestLayout();
			}
		}

		public static void UpdateHeight(this AView nativeView, IView view)
		{
			// GetDesiredSize will take the specified Height into account during the layout
			if (!nativeView.IsInLayout)
			{
				nativeView.RequestLayout();
			}
		}
	}
}
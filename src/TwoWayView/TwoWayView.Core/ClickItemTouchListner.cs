#region

using Android.Content;
using Android.OS;
using Android.Support.V7.Widget;
using Android.Views;
using Java.Lang;

#endregion

namespace TwoWayView.Core
{
	public abstract class ClickItemTouchListener : Object, RecyclerView.IOnItemTouchListener
	{
		private readonly GestureDetector _gestureDetector;


		public ClickItemTouchListener(RecyclerView hostView)
		{
			_gestureDetector = new ItemClickGestureDetector(hostView.Context, new ItemClickGestureListener(hostView, this));
		}

		public bool OnInterceptTouchEvent(RecyclerView recyclerView, MotionEvent @event)
		{
			if (!isAttachedToWindow(recyclerView) || !hasAdapter(recyclerView))
				return false;

			_gestureDetector.OnTouchEvent(@event);
			return false;
		}

		public void OnTouchEvent(RecyclerView recyclerView, MotionEvent @event)
		{
			// We can silently track tap and and long presses by silently
			// intercepting touch @events in the host RecyclerView.
		}

		public void OnRequestDisallowInterceptTouchEvent(bool disallow)
		{
		}

		private bool isAttachedToWindow(RecyclerView hostView)
		{
			if (Build.VERSION.SdkInt >= BuildVersionCodes.Kitkat)
				return hostView.IsAttachedToWindow;
			return hostView.Handler != null;
		}

		private bool hasAdapter(RecyclerView hostView)
		{
			return hostView.GetAdapter() != null;
		}

		public abstract bool PerformItemClick(RecyclerView parent, View view, int position, long id);
		public abstract bool PerformItemLongClick(RecyclerView parent, View view, int position, long id);

		private class ItemClickGestureListener : GestureDetector.SimpleOnGestureListener
		{
			private readonly RecyclerView _hostView;
			private readonly ClickItemTouchListener _owner;
			private View _targetChild;

			public ItemClickGestureListener(RecyclerView hostView, ClickItemTouchListener owner)
			{
				_hostView = hostView;
				_owner = owner;
			}

			public void DispatchSingleTapUpIfNeeded(MotionEvent @event)
			{
				// When the long press hook is called but the long press listener
				// returns false, the target child will be left around to be
				// handled later. In this case, we should still treat the gesture
				// as potential item click.
				if (_targetChild != null)
					OnSingleTapUp(@event);
			}

			public override bool OnDown(MotionEvent @event)
			{
				var x = (int) @event.GetX();
				var y = (int) @event.GetY();

				_targetChild = _hostView.FindChildViewUnder(x, y);
				return _targetChild != null;
			}

			public override void OnShowPress(MotionEvent @event)
			{
				if (_targetChild != null)
					_targetChild.Pressed = true;
			}

			public override bool OnSingleTapUp(MotionEvent @event)
			{
				var handled = false;

				if (_targetChild != null)
				{
					_targetChild.Pressed = false;

					var position = _hostView.GetChildAdapterPosition(_targetChild);
					var id = _hostView.GetAdapter().GetItemId(position);
					handled = _owner.PerformItemClick(_hostView, _targetChild, position, id);

					_targetChild = null;
				}

				return handled;
			}

			public override bool OnScroll(MotionEvent @event, MotionEvent event2, float v, float v2)
			{
				if (_targetChild != null)
				{
					_targetChild.Pressed = false;
					_targetChild = null;

					return true;
				}

				return false;
			}

			public override void OnLongPress(MotionEvent @event)
			{
				if (_targetChild == null)
					return;

				var position = _hostView.GetChildAdapterPosition(_targetChild);
				var id = _hostView.GetAdapter().GetItemId(position);
				var handled = _owner.PerformItemLongClick(_hostView, _targetChild, position, id);

				if (handled)
				{
					_targetChild.Pressed = false;
					_targetChild = null;
				}
			}
		}

		private class ItemClickGestureDetector : GestureDetector
		{
			private readonly ItemClickGestureListener _gestureListener;

			public ItemClickGestureDetector(Context context, ItemClickGestureListener listener) : base(context, listener)
			{
				_gestureListener = listener;
			}

			public override bool OnTouchEvent(MotionEvent @event)
			{
				var handled = base.OnTouchEvent(@event);

				var action = @event.Action & MotionEventActions.Mask;
				if (action == MotionEventActions.Up)
					_gestureListener.DispatchSingleTapUpIfNeeded(@event);

				return handled;
			}
		}
	}
}
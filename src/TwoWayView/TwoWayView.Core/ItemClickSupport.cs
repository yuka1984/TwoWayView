#region

using System;
using Android.Support.V7.Widget;
using Android.Views;
using Object = Java.Lang.Object;

#endregion

namespace TwoWayView.Core
{
	public class ItemClickSupport : Object

	{
		private readonly RecyclerView.IOnChildAttachStateChangeListener _mAttachListener;
		private IOnItemClickListener _onItemClickListener;
		private IOnItemLongClickListener _onItemLongClickListener;
		private readonly RecyclerView _recyclerView;

		private ItemClickSupport(RecyclerView recyclerView)
		{
			View.IOnClickListener mOnClickListener = new ViewClickListner
			{
				OnClickAction = v =>
				{
					if (_onItemClickListener != null)
					{
						var holder = _recyclerView.GetChildViewHolder(v);
						_onItemClickListener.OnItemClicked(_recyclerView, holder.AdapterPosition, v);
					}
				}
			};
			View.IOnLongClickListener mOnLongClickListener = new ViewClickListner
			{
				OnLongClickAction = v =>
				{
					if (_onItemLongClickListener != null)
					{
						var holder = _recyclerView.GetChildViewHolder(v);
						return _onItemLongClickListener.OnItemLongClicked(_recyclerView, holder.AdapterPosition, v);
					}
					return false;
				}
			};
			_mAttachListener = new OnChildAttachStateChangeListener
			{
				OnChildViewAttachedToWindowAction = view =>
				{
					if (_onItemClickListener != null)
						view.SetOnClickListener(mOnClickListener);
					if (_onItemLongClickListener != null)
						view.SetOnLongClickListener(mOnLongClickListener);
				}
			};

			_recyclerView = recyclerView;
			_recyclerView.SetTag(Resource.Id.item_click_support, this);
			_recyclerView.AddOnChildAttachStateChangeListener(_mAttachListener);
		}

		public static ItemClickSupport AddTo(RecyclerView view)
		{
			var support = (ItemClickSupport) view.GetTag(Resource.Id.item_click_support);
			if (support == null)
				support = new ItemClickSupport(view);
			return support;
		}

		public static ItemClickSupport RemoveFrom(RecyclerView view)
		{
			var support = (ItemClickSupport) view.GetTag(Resource.Id.item_click_support);
			support?.Detach(view);
			return support;
		}

		public ItemClickSupport SetOnItemClickListener(IOnItemClickListener listener)
		{
			_onItemClickListener = listener;
			return this;
		}

		public ItemClickSupport SetOnItemLongClickListener(IOnItemLongClickListener listener)
		{
			_onItemLongClickListener = listener;
			return this;
		}

		private void Detach(RecyclerView view)
		{
			view.RemoveOnChildAttachStateChangeListener(_mAttachListener);
			view.SetTag(Resource.Id.item_click_support, null);
		}

		public interface IOnItemClickListener
		{
			void OnItemClicked(RecyclerView recyclerView, int position, View v);
		}

		public class OnItemClickListner : IOnItemClickListener
		{
			public Action<RecyclerView, int, View> OnItemClickedAction { get; set; }
			public void OnItemClicked(RecyclerView recyclerView, int position, View v)
			{
				OnItemClickedAction?.Invoke(recyclerView, position, v);
			}
		}

		public interface IOnItemLongClickListener
		{
			bool OnItemLongClicked(RecyclerView recyclerView, int position, View v);
		}

		public class OnItemLongClickListener : IOnItemLongClickListener
		{
			public Func<RecyclerView, int, View, bool> OnItemLongClickedFunc { get; set; }
			public bool OnItemLongClicked(RecyclerView recyclerView, int position, View v)
			{
				return OnItemLongClickedFunc?.Invoke(recyclerView, position, v) ?? false;
			}
		}
		public class ViewClickListner : Object, View.IOnClickListener, View.IOnLongClickListener
		{
			public Action<View> OnClickAction { get; set; }

			public Func<View, bool> OnLongClickAction { get; set; }

			void View.IOnClickListener.OnClick(View v)
			{
				OnClickAction?.Invoke(v);
			}

			bool View.IOnLongClickListener.OnLongClick(View v)
			{
				return OnLongClickAction?.Invoke(v) ?? false;
			}
		}

		public class OnChildAttachStateChangeListener : Object,
			RecyclerView.IOnChildAttachStateChangeListener
		{
			public Action<View> OnChildViewAttachedToWindowAction { get; set; }

			public Action<View> OnChildViewDetachedFromWindowAction { get; set; }

			void RecyclerView.IOnChildAttachStateChangeListener.OnChildViewAttachedToWindow(View view)
			{
				OnChildViewAttachedToWindowAction?.Invoke(view);
			}

			void RecyclerView.IOnChildAttachStateChangeListener.OnChildViewDetachedFromWindow(View view)
			{
				OnChildViewDetachedFromWindowAction?.Invoke(view);
			}
		}

		public class OnItemClickListener : ItemClickSupport.IOnItemClickListener,
			ItemClickSupport.IOnItemLongClickListener
		{
			public Action<RecyclerView, int, View> OnItemClickedAction { get; set; }

			public Func<RecyclerView, int, View, bool> OnItemLongClickedAction { get; set; }

			public void OnItemClicked(RecyclerView recyclerView, int position, View v)
			{
				OnItemClickedAction?.Invoke(recyclerView, position, v);
			}

			public bool OnItemLongClicked(RecyclerView recyclerView, int position, View v)
			{
				return OnItemLongClickedAction?.Invoke(recyclerView, position, v) ?? false;
			}
		}


	}
}
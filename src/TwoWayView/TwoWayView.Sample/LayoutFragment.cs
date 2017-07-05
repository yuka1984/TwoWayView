#region

using Android.App;
using Android.OS;
using Android.Support.V4.Content.Res;
using Android.Support.V7.Widget;
using Android.Views;
using Android.Widget;
using TwoWayView.Core;
using DividerItemDecoration = TwoWayView.Layout.DividerItemDecoration;
using Fragment = Android.Support.V4.App.Fragment;

#endregion

namespace TwoWayView.Sample
{
	public class LayoutFragment : Fragment
	{
		private static readonly string ARG_LAYOUT_ID = "layout_id";
		private TextView _countText;

		private int _layoutId;
		private TextView _positionText;

		private Layout.TwoWayView _recyclerView;
		private TextView _stateText;
		private Toast _toast;

		public static LayoutFragment NewInstance(int layoutId)
		{
			var fragment = new LayoutFragment();

			var args = new Bundle();
			args.PutInt(ARG_LAYOUT_ID, layoutId);
			fragment.Arguments = args;

			return fragment;
		}


		public override void OnCreate(Bundle savedInstanceState)
		{
			base.OnCreate(savedInstanceState);
			_layoutId = Arguments.GetInt(ARG_LAYOUT_ID);
		}


		public override View OnCreateView(LayoutInflater inflater, ViewGroup container,
			Bundle savedInstanceState)
		{
			return inflater.Inflate(_layoutId, container, false);
		}


		public override void OnViewCreated(View view, Bundle savedInstanceState)
		{
			base.OnViewCreated(view, savedInstanceState);

			Activity activity = Activity;

			_toast = Toast.MakeText(activity, "", ToastLength.Long);
			_toast.SetGravity(GravityFlags.Center, 0, 0);

			_recyclerView = (Layout.TwoWayView) view.FindViewById(Resource.Id.list);
			_recyclerView.HasFixedSize = true;
			_recyclerView.LongClickable = true;

			_positionText = (TextView) view.RootView.FindViewById(Resource.Id.position);
			_countText = (TextView) view.RootView.FindViewById(Resource.Id.count);

			_stateText = (TextView) view.RootView.FindViewById(Resource.Id.state);
			UpdateState(RecyclerView.ScrollStateIdle);

			var itemClick = ItemClickSupport.AddTo(_recyclerView);

			itemClick.SetOnItemClickListener(new ItemClickSupport.OnItemClickListener
			{
				OnItemClickedAction = (parent, position, v) =>
				{
					_toast.SetText("Item clicked: " + position);
					_toast.Show();
				}
			});

			itemClick.SetOnItemLongClickListener(new ItemClickSupport.OnItemClickListener()
			{
				OnItemLongClickedAction = (parent, position, v) =>
				{
					_toast.SetText("Item long clicked: " + position);
					_toast.Show();
					return true;
				}
			});

			var divider = ResourcesCompat.GetDrawable(Resources, Resource.Drawable.divider, null);
			_recyclerView.AddItemDecoration(new DividerItemDecoration(divider));

			_recyclerView.SetAdapter(new LayoutAdapter(activity, _recyclerView, _layoutId));
		}

		private void UpdateState(int scrollState)
		{
			var stateName = "Undefined";
			switch (scrollState)
			{
				case RecyclerView.ScrollStateIdle:
					stateName = "Idle";
					break;

				case RecyclerView.ScrollStateDragging:
					stateName = "Dragging";
					break;

				case RecyclerView.ScrollStateSettling:
					stateName = "Flinging";
					break;
			}

			_stateText.Text = stateName;
		}

		public int GetLayoutId()
		{
			return Arguments.GetInt(ARG_LAYOUT_ID);
		}
	}
}
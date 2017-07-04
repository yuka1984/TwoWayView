#region

using Android.OS;
using Android.Support.V7.Widget;
using Android.Util;
using Android.Views;
using Android.Widget;
using Java.Lang;
using Math = System.Math;

#endregion

namespace TwoWayView.Core
{
	public class ItemSelectionSupport : Object
	{
		public static int INVALID_POSITION = -1;

		private static readonly string STATE_KEY_CHOICE_MODE = "choiceMode";
		private static readonly string STATE_KEY_CHECKED_STATES = "CheckedStates";
		private static readonly string STATE_KEY_CHECKED_ID_STATES = "CheckedIdStates";
		private static readonly string STATE_KEY_CHECKED_COUNT = "CheckedCount";

		private static readonly int CHECK_POSITION_SEARCH_DISTANCE = 20;


		private readonly RecyclerView _recyclerView;
		private readonly TouchListener _touchListener;
		private int _checkedCount;
		private TwoWayLayoutManager.CheckedIdStates _checkedIdStates;
		private TwoWayLayoutManager.CheckedStates _checkedStates;

		private ChoiceMode _choiceMode = ChoiceMode.None;

		private ItemSelectionSupport(RecyclerView recyclerView)
		{
			_recyclerView = recyclerView;

			_touchListener = new TouchListener(recyclerView, this);
			recyclerView.AddOnItemTouchListener(_touchListener);
		}

		private void UpdateOnScreenCheckedViews()
		{
			var count = _recyclerView.ChildCount;
			for (var i = 0; i < count; i++)
			{
				var child = _recyclerView.GetChildAt(i);
				var position = _recyclerView.GetChildAdapterPosition(child);
				setViewChecked(child, _checkedStates.Get(position));
			}
		}

		/**
		 * Returns the number of items currently selected. This will only be valid
		 * if the choice mode is not {@link ChoiceMode#NONE} (default).
		 *
		 * <p>To determine the specific items that are currently selected, use one of
		 * the <code>getChecked*</code> methods.
		 *
		 * @return The number of items currently selected
		 *
		 * @see #getCheckedItemPosition()
		 * @see #getCheckedItemPositions()
		 * @see #getCheckedItemIds()
		 */
		public int GetCheckedItemCount()
		{
			return _checkedCount;
		}

		/**
		 * Returns the Checked state of the specified position. The result is only
		 * valid if the choice mode has been set to {@link ChoiceMode#SINGLE}
		 * or {@link ChoiceMode#MULTIPLE}.
		 *
		 * @param position The item whose Checked state to return
		 * @return The item's Checked state or <code>false</code> if choice mode
		 *         is invalid
		 *
		 * @see #setChoiceMode(ChoiceMode)
		 */
		public bool IsItemChecked(int position)
		{
			if (_choiceMode != ChoiceMode.None && _checkedStates != null)
				return _checkedStates.Get(position);

			return false;
		}

		/**
		 * Returns the currently Checked item. The result is only valid if the choice
		 * mode has been set to {@link ChoiceMode#SINGLE}.
		 *
		 * @return The position of the currently Checked item or
		 *         {@link #INVALID_POSITION} if nothing is selected
		 *
		 * @see #setChoiceMode(ChoiceMode)
		 */
		public int GetCheckedItemPosition()
		{
			if (_choiceMode == ChoiceMode.Single && _checkedStates != null && _checkedStates.Size() == 1)
				return _checkedStates.KeyAt(0);

			return INVALID_POSITION;
		}

		/**
		 * Returns the set of Checked items in the list. The result is only valid if
		 * the choice mode has not been set to {@link ChoiceMode#NONE}.
		 *
		 * @return  A SparseBooleanArray which will return true for each call to
		 *          get(int position) where position is a position in the list,
		 *          or <code>null</code> if the choice mode is set to
		 *          {@link ChoiceMode#NONE}.
		 */
		public SparseBooleanArray GetCheckedItemPositions()
		{
			if (_choiceMode != ChoiceMode.None)
				return _checkedStates;

			return null;
		}

		/**
		 * Returns the set of Checked items ids. The result is only valid if the
		 * choice mode has not been set to {@link ChoiceMode#NONE} and the adapter
		 * has stable IDs.
		 *
		 * @return A new array which contains the id of each Checked item in the
		 *         list.
		 *
		 * @see android.support.v7.widget.RecyclerView.Adapter#HasStableIds
		 */
		public long[] GetCheckedItemIds()
		{
			if (_choiceMode == ChoiceMode.None
			    || _checkedIdStates == null || _recyclerView.GetAdapter() == null)
				return new long[0];

			var count = _checkedIdStates.Size();
			var ids = new long[count];

			for (var i = 0; i < count; i++)
				ids[i] = _checkedIdStates.KeyAt(i);

			return ids;
		}

		/**
		 * Sets the Checked state of the specified position. The is only valid if
		 * the choice mode has been set to {@link ChoiceMode#SINGLE} or
		 * {@link ChoiceMode#MULTIPLE}.
		 *
		 * @param position The item whose Checked state is to be Checked
		 * @param Checked The new Checked state for the item
		 */
		public void SetItemChecked(int position, bool Checked)
		{
			if (_choiceMode == ChoiceMode.None)
				return;

			var adapter = _recyclerView.GetAdapter();

			if (_choiceMode == ChoiceMode.Multiple)
			{
				var oldValue = _checkedStates.Get(position);
				_checkedStates.Put(position, Checked);

				if (_checkedIdStates != null && adapter.HasStableIds)
					if (Checked)
						_checkedIdStates.Put(adapter.GetItemId(position), position);
					else
						_checkedIdStates.Delete(adapter.GetItemId(position));

				if (oldValue != Checked)
					if (Checked)
						_checkedCount++;
					else
						_checkedCount--;
			}
			else
			{
				var updateIds = _checkedIdStates != null && adapter.HasStableIds;

				// Clear all values if we're checking something, or unchecking the currently
				// selected item
				if (Checked || IsItemChecked(position))
				{
					_checkedStates.Clear();

					if (updateIds)
						_checkedIdStates.Clear();
				}

				// This may end up selecting the Checked we just cleared but this way
				// we ensure length of mCheckStates is 1, a fact getCheckedItemPosition relies on
				if (Checked)
				{
					_checkedStates.Put(position, true);

					if (updateIds)
						_checkedIdStates.Put(adapter.GetItemId(position), position);

					_checkedCount = 1;
				}
				else if (_checkedStates.Size() == 0 || !_checkedStates.ValueAt(0))
				{
					_checkedCount = 0;
				}
			}

			UpdateOnScreenCheckedViews();
		}


		public void setViewChecked(View view, bool Checked)
		{
			if (view is ICheckable)
				((ICheckable) view).Checked = Checked;
			else if (Build.VERSION.SdkInt >= BuildVersionCodes.Honeycomb)
				view.Activated = Checked;
		}

		/**
		 * Clears any choices previously set.
		 */
		public void ClearChoices()
		{
			if (_checkedStates != null)
				_checkedStates.Clear();

			if (_checkedIdStates != null)
				_checkedIdStates.Clear();

			_checkedCount = 0;
			UpdateOnScreenCheckedViews();
		}

		/**
		 * Returns the current choice mode.
		 *
		 * @see #setChoiceMode(ChoiceMode)
		 */
		public ChoiceMode GetChoiceMode()
		{
			return _choiceMode;
		}

		/**
		 * Defines the choice behavior for the List. By default, Lists do not have any choice behavior
		 * ({@link ChoiceMode#NONE}). By setting the choiceMode to {@link ChoiceMode#SINGLE}, the
		 * List allows up to one item to  be in a chosen state. By setting the choiceMode to
		 * {@link ChoiceMode#MULTIPLE}, the list allows any number of items to be chosen.
		 *
		 * @param choiceMode One of {@link ChoiceMode#NONE}, {@link ChoiceMode#SINGLE}, or
		 * {@link ChoiceMode#MULTIPLE}
		 */
		public void SetChoiceMode(ChoiceMode choiceMode)
		{
			if (_choiceMode == choiceMode)
				return;

			_choiceMode = choiceMode;

			if (_choiceMode != ChoiceMode.None)
			{
				if (_checkedStates == null)
					_checkedStates = new TwoWayLayoutManager.CheckedStates();

				var adapter = _recyclerView.GetAdapter();
				if (_checkedIdStates == null && adapter != null && adapter.HasStableIds)
					_checkedIdStates = new TwoWayLayoutManager.CheckedIdStates();
			}
		}

		public void OnAdapterDataChanged()
		{
			var adapter = _recyclerView.GetAdapter();
			if (_choiceMode == ChoiceMode.None || adapter == null || !adapter.HasStableIds)
				return;

			var itemCount = adapter.ItemCount;

			// Clear out the positional check states, we'll rebuild it below from IDs.
			_checkedStates.Clear();

			for (var checkedIndex = 0; checkedIndex < _checkedIdStates.Size(); checkedIndex++)
			{
				var currentId = _checkedIdStates.KeyAt(checkedIndex);
				var currentPosition = (int) _checkedIdStates.ValueAt(checkedIndex);

				var newPositionId = adapter.GetItemId(currentPosition);
				if (currentId != newPositionId)
				{
					// Look around to see if the ID is nearby. If not, uncheck it.
					var start = Math.Max(0, currentPosition - CHECK_POSITION_SEARCH_DISTANCE);
					var end = Math.Min(currentPosition + CHECK_POSITION_SEARCH_DISTANCE, itemCount);

					var found = false;
					for (var searchPos = start; searchPos < end; searchPos++)
					{
						var searchId = adapter.GetItemId(searchPos);
						if (currentId == searchId)
						{
							found = true;
							_checkedStates.Put(searchPos, true);
							_checkedIdStates.SetValueAt(checkedIndex, searchPos);
							break;
						}
					}

					if (!found)
					{
						_checkedIdStates.Delete(currentId);
						_checkedCount--;
						checkedIndex--;
					}
				}
				else
				{
					_checkedStates.Put(currentPosition, true);
				}
			}
		}

		public Bundle OnSaveInstanceState()
		{
			var state = new Bundle();

			state.PutInt(STATE_KEY_CHOICE_MODE, (int) _choiceMode);
			state.PutParcelable(STATE_KEY_CHECKED_STATES, _checkedStates);
			state.PutParcelable(STATE_KEY_CHECKED_ID_STATES, _checkedIdStates);
			state.PutInt(STATE_KEY_CHECKED_COUNT, _checkedCount);

			return state;
		}

		public void OnRestoreInstanceState(Bundle state)
		{
			_choiceMode = (ChoiceMode) state.GetInt(STATE_KEY_CHOICE_MODE);
			_checkedStates = (TwoWayLayoutManager.CheckedStates) state.GetParcelable(STATE_KEY_CHECKED_STATES);
			_checkedIdStates = (TwoWayLayoutManager.CheckedIdStates) state.GetParcelable(STATE_KEY_CHECKED_ID_STATES);
			_checkedCount = state.GetInt(STATE_KEY_CHECKED_COUNT);

			// TODO confirm ids here
		}

		public static ItemSelectionSupport AddTo(RecyclerView recyclerView)
		{
			var itemSelectionSupport = From(recyclerView);
			if (itemSelectionSupport == null)
			{
				itemSelectionSupport = new ItemSelectionSupport(recyclerView);
				recyclerView.SetTag(Resource.Id.twowayview_item_selection_support, itemSelectionSupport);
			}

			return itemSelectionSupport;
		}

		public static void RemoveFrom(RecyclerView recyclerView)
		{
			var itemSelection = From(recyclerView);
			if (itemSelection == null)
				return;

			itemSelection.ClearChoices();

			recyclerView.RemoveOnItemTouchListener(itemSelection._touchListener);
			recyclerView.SetTag(Resource.Id.twowayview_item_selection_support, null);
		}

		public static ItemSelectionSupport From(RecyclerView recyclerView)
		{
			if (recyclerView == null)
				return null;

			return (ItemSelectionSupport) recyclerView.GetTag(Resource.Id.twowayview_item_selection_support);
		}

		private class TouchListener : ClickItemTouchListener
		{
			private readonly ItemSelectionSupport _owner;

			public TouchListener(RecyclerView recyclerView, ItemSelectionSupport owner) : base(recyclerView)
			{
				_owner = owner;
			}

			public override bool PerformItemClick(RecyclerView parent, View view, int position, long id)
			{
				var adapter = _owner._recyclerView.GetAdapter();
				var checkedStateChanged = false;

				if (_owner._choiceMode == ChoiceMode.Multiple)
				{
					var check = !_owner._checkedStates.Get(position, false);

					_owner._checkedStates.Put(position, check);

					if (_owner._checkedIdStates != null && adapter.HasStableIds)
						if (check)
							_owner._checkedIdStates.Put(adapter.GetItemId(position), position);
						else
							_owner._checkedIdStates.Delete(adapter.GetItemId(position));

					if (check)
						_owner._checkedCount++;
					else
						_owner._checkedCount--;

					checkedStateChanged = true;
				}
				else if (_owner._choiceMode == ChoiceMode.Single)
				{
					var check = !_owner._checkedStates.Get(position, false);
					if (check)
					{
						_owner._checkedStates.Clear();
						_owner._checkedStates.Put(position, true);

						if (_owner._checkedIdStates != null && adapter.HasStableIds)
						{
							_owner._checkedIdStates.Clear();
							_owner._checkedIdStates.Put(adapter.GetItemId(position), position);
						}

						_owner._checkedCount = 1;
					}
					else if (_owner._checkedStates.Size() == 0 || !_owner._checkedStates.ValueAt(0))
					{
						_owner._checkedCount = 0;
					}

					checkedStateChanged = true;
				}

				if (checkedStateChanged)
					_owner.UpdateOnScreenCheckedViews();

				return false;
			}


			public override bool PerformItemLongClick(RecyclerView parent, View view, int position, long id)
			{
				return true;
			}
		}
	}
}
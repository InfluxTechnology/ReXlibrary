using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Influx.Shared.Objects
{
    public class SortableBindingList<T> : BindingList<T>
    {
        private bool isSorted;
        private ListSortDirection sortDirection;
        private PropertyDescriptor sortProperty;

        protected override bool SupportsSortingCore => true;
        protected override bool IsSortedCore => isSorted;
        protected override PropertyDescriptor SortPropertyCore => sortProperty;
        protected override ListSortDirection SortDirectionCore => sortDirection;

        public SortableBindingList() : base() { }

        public SortableBindingList(IList<T> list) : base(list) { }

        protected override void ApplySortCore(PropertyDescriptor prop, ListSortDirection direction)
        {
            var items = (List<T>)Items;
            items.Sort((x, y) =>
            {
                object xValue = prop.GetValue(x);
                object yValue = prop.GetValue(y);
                return direction == ListSortDirection.Ascending
                    ? Comparer<object>.Default.Compare(xValue, yValue)
                    : Comparer<object>.Default.Compare(yValue, xValue);
            });

            isSorted = true;
            sortProperty = prop;
            sortDirection = direction;
            OnListChanged(new ListChangedEventArgs(ListChangedType.Reset, -1));
        }

        public void Sort(Comparison<T> comparison)
        {
            if (isSorted)
                ReapplySort();
            else
            {

                var items = (List<T>)Items;
                items.Sort(comparison);
                OnListChanged(new ListChangedEventArgs(ListChangedType.Reset, -1));
            }
        }

        public void AddRange(IEnumerable<T> items)
        {
            if (items == null)
                return;

            RaiseListChangedEvents = false;

            foreach (var item in items)
            {
                Add(item);
            }

            RaiseListChangedEvents = true;
            ReapplySort();
            //ResetBindings();
        }

        public int RemoveAll(Predicate<T> match)
        {
            int removedCount = 0;

            RaiseListChangedEvents = false;
            for (int i = Count - 1; i >= 0; i--)
            {
                if (match(this[i]))
                {
                    RemoveAt(i);
                    removedCount++;
                }
            }
            RaiseListChangedEvents = true;

            ReapplySort();
            return removedCount;
        }

        private void ReapplySort()
        {
            if (isSorted && sortProperty != null)
                ApplySortCore(sortProperty, sortDirection);
            else
                OnListChanged(new ListChangedEventArgs(ListChangedType.Reset, -1));
        }
    }
}
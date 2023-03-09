using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SvnSummaryTool.Model
{
    internal class CanCheckedItem<T>
    {
        public bool IsChecked { get; set; }

        public T Item { get; set; }

        public CanCheckedItem(T item)
        {
            this.IsChecked = false;
            this.Item = item;
        }

        public CanCheckedItem(T item, bool isChecked)
        {
            this.IsChecked = isChecked;
            this.Item = item;
        }
    }
}

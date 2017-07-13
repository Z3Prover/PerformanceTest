using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace PerformanceTest.Management
{
    public class BrowseTreeViewModel
    {
        private readonly BrowseTreeItemViewModel[] root;
        private readonly string title;

        public BrowseTreeViewModel(string title, BrowseTreeItemViewModel[] root)
        {
            if (root == null) throw new ArgumentNullException("root");
            this.root = root;
            this.title = title;
        }

        public BrowseTreeItemViewModel[] Tree { get { return root; } }

        public string Title { get { return title; } }

        public BrowseTreeItemViewModel[] SelectedPath
        {
            get; set;
        }

        public async Task Select(string[] selected)
        {
            IEnumerable<BrowseTreeItemViewModel> level = root;
            foreach (string item in selected)
            {
                IEnumerable<BrowseTreeItemViewModel> nextLevel = null;
                foreach (var levelItem in level)
                {
                    if(levelItem.Text == item)
                    {
                        await levelItem.Expand();
                        levelItem.IsSelected = true;
                        nextLevel = levelItem.Children;
                        break;
                    }
                }
                if (nextLevel == null) break;
                level = nextLevel;
            }
        }
    }


    public delegate Task<BrowseTreeItemViewModel[]> GetChildren(BrowseTreeItemViewModel parent);

    public class BrowseTreeItemViewModel : INotifyPropertyChanged
    {
        private readonly string text;
        private readonly BrowseTreeItemViewModel parent;
        private readonly ObservableCollection<BrowseTreeItemViewModel> children;
        private bool isExpanded;
        private bool isSelected;
        private GetChildren lazyChildren;

        public event PropertyChangedEventHandler PropertyChanged;


        public BrowseTreeItemViewModel(string text, GetChildren children = null)
        {
            this.text = text;
            this.lazyChildren = children;
            this.children = new ObservableCollection<BrowseTreeItemViewModel>();
            if (children != null)
            {
                this.children.Add(new BrowseTreeItemViewModel("Loading...")); // dummy child
            }
        }

        public BrowseTreeItemViewModel(string text, BrowseTreeItemViewModel parent, GetChildren children = null) : this(text, children)
        {
            this.parent = parent;
        }

        public ObservableCollection<BrowseTreeItemViewModel> Children { get { return children; } }

        public string Text { get { return text; } }

        public BrowseTreeItemViewModel Parent { get { return parent; } }

        public bool IsExpanded
        {
            get { return isExpanded; }
            set
            {
                if (value != isExpanded)
                {
                    isExpanded = value;
                    NotifyPropertyChanged();
                }

                if (isExpanded && parent != null)
                    parent.IsExpanded = true;

                if (isExpanded && lazyChildren != null)
                {
                    var _ = PopulateChildrenAsync(lazyChildren);
                    lazyChildren = null;
                }
            }
        }

        public async Task Expand()
        {
            if (!IsActualized)
            {
                var lazy = lazyChildren;
                lazyChildren = null;
                await PopulateChildrenAsync(lazy);
            }
            IsExpanded = true;
        }

        public bool IsSelected
        {
            get { return isSelected; }
            set
            {
                if (isSelected == value) return;
                isSelected = value;
                NotifyPropertyChanged();
            }
        }

        public bool IsActualized
        {
            get { return lazyChildren == null; }
        }

        private async Task PopulateChildrenAsync(GetChildren lazyChildren)
        {
            var c = await lazyChildren(this);
            children.Clear();
            foreach (var item in c)
            {
                children.Add(item);
            }
        }

        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

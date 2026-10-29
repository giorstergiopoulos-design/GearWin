using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using OptimizerWpf.Services;

namespace OptimizerWpf.Views
{
    public partial class CompositeTabView : UserControl
    {
        private readonly string _tag;
        private readonly IReadOnlyList<TabSubPage> _pages;
        private readonly Dictionary<int, List<UserControl>> _pageViews = new();
        private int _current = -1;

        public int CurrentPage => _current;

        public CompositeTabView(string tag, int startPage = 0)
        {
            InitializeComponent();
            _tag = tag;
            _pages = TabLayout.SubPages(tag) ?? throw new ArgumentException("Not a composite tab: " + tag);

            if (_pages.Count > 1)
            {
                SubStrip.Visibility = Visibility.Visible;
                for (var i = 0; i < _pages.Count; i++)
                {
                    var index = i;
                    var chip = new RadioButton
                    {
                        Content = LanguageService.T(_pages[i].TitleKey),
                        GroupName = "SubPages_" + tag,
                        Style = (Style)FindResource("SubPageChipStyle"),
                        Tag = index,
                    };
                    chip.Checked += (_, _) => ShowPage(index);
                    SubStrip.Children.Add(chip);
                }
            }

            ShowPage(Math.Clamp(startPage, 0, _pages.Count - 1));
            if (SubStrip.Children.Count > _current) ((RadioButton)SubStrip.Children[_current]).IsChecked = true;
        }

        private void ShowPage(int index)
        {
            if (index == _current) return;
            _current = index;
            PageHost.Children.Clear();
            PageScroll.ScrollToTop();

            if (!_pageViews.TryGetValue(index, out var views))
            {
                views = BuildViews(_pages[index]);
                _pageViews[index] = views;
            }
            for (var i = 0; i < views.Count; i++)
            {
                views[i].Margin = new Thickness(0, i == 0 ? 0 : 16, 0, 0);   // ίδιο κενό 16 px και ανάμεσα στις προβολές μιας σελίδας
                PageHost.Children.Add(views[i]);
            }
        }

        // Μία παρουσία ανά προβολή (με την ένωση των ενοτήτων που ζητούν τα τμήματα της σελίδας), με τη σειρά πρώτης εμφάνισης.
        private static List<UserControl> BuildViews(TabSubPage page)
        {
            var result = new List<UserControl>();
            foreach (var group in page.Parts.GroupBy(p => p.View))
            {
                var view = TabLayout.CreateView(group.Key);
                var keep = new HashSet<string>(group.SelectMany(p => p.Sections));
                TabLayout.ApplySections(view, group.Key, keep);
                if (view.Content is ScrollViewer sv)
                {
                    sv.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
                    sv.AddHandler(PreviewMouseWheelEvent, new MouseWheelEventHandler(NestedScrollHelper.Forward));
                }
                result.Add(view);
            }
            return result;
        }
    }
}

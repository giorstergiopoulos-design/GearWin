using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using OptimizerWpf.Services;
using OptimizerWpf.Views;

namespace OptimizerWpf.Tests
{
    // 6.5.0 - η νέα δομή καρτελών (TabLayout) βασίζεται στη θέση κάθε κάρτας στη ρίζα της προβολής. Αυτά τα tests αποτυγχάνουν αν
    // κάποιος προσθέσει/αφαιρέσει κάρτα στο XAML χωρίς να ενημερώσει τον πίνακα ενοτήτων (αλλιώς θα εμφανιζόταν σε λάθος καρτέλα).
    public class TabLayoutTests
    {
        private static void RunSta(Action body)
        {
            Exception? error = null;
            var t = new Thread(() =>
            {
                try
                {
                    if (Application.Current == null) { var app = new OptimizerWpf.App(); app.InitializeComponent(); }
                    OptimizerWpf.ThemeManager.LoadPersisted();
                    body();
                }
                catch (Exception ex) { error = ex; }
            });
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            t.Join();
            if (error != null) throw new Exception(error.Message, error);
        }

        [Fact]
        public void SectionTablesMatchTheXamlOfEveryView()
        {
            RunSta(() =>
            {
                var problems = new List<string>();
                foreach (var key in TabLayout.ViewsWithSections)
                {
                    var view = TabLayout.CreateView(key);
                    var panel = TabLayout.RootPanel(view);
                    if (panel == null) { problems.Add($"{key}: no root StackPanel"); continue; }
                    if (panel.Children.Count != TabLayout.RootChildCount(key))
                        problems.Add($"{key}: XAML has {panel.Children.Count} root children, TabLayout lists {TabLayout.RootChildCount(key)}");
                }
                Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
            });
        }

        [Fact]
        public void EveryPageReferencesExistingSectionsAndShowsContent()
        {
            RunSta(() =>
            {
                var problems = new List<string>();
                foreach (var tag in TabLayout.CompositeTags)
                {
                    var pages = TabLayout.SubPages(tag)!;
                    for (var p = 0; p < pages.Count; p++)
                    {
                        CompositeTabView view;
                        try { view = new CompositeTabView(tag, p); }
                        catch (Exception ex) { problems.Add($"{tag}#{p}: {ex.GetType().Name} {ex.Message}"); continue; }
                        var host = (StackPanel)view.FindName("PageHost");
                        var visibleCards = host.Children.OfType<UserControl>().Sum(v => TabLayout.RootPanel(v)?.Children.Count ?? 0);
                        if (visibleCards == 0) problems.Add($"{tag}#{p} ({pages[p].TitleKey}) shows no cards - a section name is probably misspelled");
                        if (LanguageService.T(pages[p].TitleKey) == pages[p].TitleKey) problems.Add($"{pages[p].TitleKey} has no translation");
                    }
                }
                Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
            });
        }

        // 6.5.4: οι κάρτες συσκευών (οθόνες, ήχος, κάμερα/μικρόφωνο) ζουν στο Σύστημα › Συσκευές, όχι πια στα Παιχνίδια.
        [Fact]
        public void DeviceCardsMovedFromGamesToSystemDevices()
        {
            RunSta(() =>
            {
                Assert.Equal(new[] { "Sub_Info", "Sub_Devices", "Sub_Tools" }, TabLayout.SubPages("System")!.Select(p => p.TitleKey).ToArray());
                Assert.Equal(new[] { "Sub_Games", "Sub_Media" }, TabLayout.SubPages("GamesMedia")!.Select(p => p.TitleKey).ToArray());
                var devices = new CompositeTabView("System", 1);
                var games = new CompositeTabView("GamesMedia", 0);
                var media = new CompositeTabView("GamesMedia", 1);
                int Cards(CompositeTabView v) => ((StackPanel)v.FindName("PageHost")).Children.OfType<UserControl>().Sum(c => TabLayout.RootPanel(c)?.Children.Count ?? 0);
                Assert.Equal(3, Cards(devices));   // οθόνες, ήχος, κάμερα/μικρόφωνο
                Assert.Equal(6, Cards(games));     // βιβλιοθήκη, ανά παιχνίδι, gaming, shader, save backup, σημείωση ενημερώσεων
                Assert.Equal(2, Cards(media));     // κωδικοποιητές, μετατροπέας
            });
        }

        [Fact]
        public void EveryCardOfTheComposedViewsAppearsInExactlyOnePage()
        {
            // Μια κάρτα που δεν εμφανίζεται πουθενά (ή εμφανίζεται δύο φορές) σημαίνει λάθος στον πίνακα ενοτήτων.
            RunSta(() =>
            {
                var problems = new List<string>();
                var used = new Dictionary<string, List<string>>();
                foreach (var tag in TabLayout.CompositeTags)
                    foreach (var page in TabLayout.SubPages(tag)!)
                        foreach (var part in page.Parts)
                            foreach (var s in part.Sections)
                            {
                                var k = part.View + ":" + s;
                                if (!used.TryGetValue(k, out var list)) used[k] = list = new List<string>();
                                list.Add(tag + "/" + page.TitleKey);
                            }
                foreach (var (k, where) in used)
                    if (where.Count > 1) problems.Add($"{k} is used by several pages: {string.Join(", ", where)}");
                foreach (var view in TabLayout.ViewsWithSections)
                    foreach (var section in TabLayout.SectionsOf(view))
                        if (!used.ContainsKey(view + ":" + section)) problems.Add($"{view}:{section} cards are not shown on any page");
                Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
            });
        }
    }
}

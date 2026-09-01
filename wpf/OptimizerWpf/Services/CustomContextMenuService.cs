using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;

namespace OptimizerWpf.Services
{
    public record CustomContextMenuItem(string Id, string MenuText, string Command, string Scope);

    // Port του cardCustomMenu (Optimizer.ps1 ~17143+) - χρήστης-ορισμένες επιλογές δεξιού κλικ. JSON
    // store παρακολουθεί ΜΟΝΟ entries που έφτιαξε η ίδια η εφαρμογή, ώστε να αφαιρούνται με ασφάλεια
    // χωρίς να αγγίζουν προϋπάρχοντα shell verbs τρίτων.
    public static class CustomContextMenuService
    {
        private static readonly string StorePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OptimizerWpf", "CustomContextMenu.json");

        public static List<CustomContextMenuItem> Load()
        {
            try
            {
                if (File.Exists(StorePath))
                    return JsonSerializer.Deserialize<List<CustomContextMenuItem>>(File.ReadAllText(StorePath)) ?? new();
            }
            catch { }
            return new();
        }

        private static void Save(List<CustomContextMenuItem> items)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(StorePath)!);
                File.WriteAllText(StorePath, JsonSerializer.Serialize(items));
            }
            catch { }
        }

        private static string RegPath(string scope, string id) => scope switch
        {
            "FolderBackground" => $@"Directory\Background\shell\{id}",
            "Files" => $@"*\shell\{id}",
            _ => $@"Directory\shell\{id}",
        };

        public static CustomContextMenuItem Add(string menuText, string command, string scope)
        {
            var id = "OptimizerCustom_" + Guid.NewGuid().ToString("N")[..10];
            if (!command.Contains("%1") && !command.Contains("%V")) command += scope == "FolderBackground" ? " \"%V\"" : " \"%1\"";

            using var key = Registry.ClassesRoot.CreateSubKey(RegPath(scope, id), writable: true);
            key?.SetValue("", menuText);
            using var cmdKey = Registry.ClassesRoot.CreateSubKey(RegPath(scope, id) + @"\command", writable: true);
            cmdKey?.SetValue("", command);

            var items = Load();
            var item = new CustomContextMenuItem(id, menuText, command, scope);
            items.Add(item);
            Save(items);
            return item;
        }

        public static void Remove(CustomContextMenuItem item)
        {
            try { Registry.ClassesRoot.DeleteSubKeyTree(RegPath(item.Scope, item.Id), throwOnMissingSubKey: false); } catch { }
            var items = Load();
            items.RemoveAll(i => i.Id == item.Id);
            Save(items);
        }
    }
}

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace PoliCoLauncherApp.Services
{
    public class HistoryEntry
    {
        [JsonProperty("date")]           public string Date           { get; set; } = "";
        [JsonProperty("connected_at")]   public string ConnectedAt    { get; set; } = "";
        [JsonProperty("disconnected_at")]public string DisconnectedAt { get; set; } = "";
        [JsonProperty("route_from")]     public string RouteFrom      { get; set; } = "";
        [JsonProperty("route_to")]       public string RouteTo        { get; set; } = "";
        [JsonProperty("train_type")]     public string TrainType      { get; set; } = "";
        [JsonProperty("train_number")]   public string TrainNumber    { get; set; } = "";
        [JsonProperty("locomotive")]     public string Locomotive     { get; set; } = "";
        [JsonProperty("wagon_count")]    public int    WagonCount     { get; set; }
    }

    public static class HistoryService
    {
        private static string FilePath =>
            Path.Combine(AppContext.BaseDirectory, "History", "history.json");

        public static List<HistoryEntry> Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return new List<HistoryEntry>();
                string json = File.ReadAllText(FilePath);
                return JsonConvert.DeserializeObject<List<HistoryEntry>>(json) ?? new List<HistoryEntry>();
            }
            catch { return new List<HistoryEntry>(); }
        }

        private static void Save(List<HistoryEntry> entries)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                File.WriteAllText(FilePath, JsonConvert.SerializeObject(entries, Formatting.Indented));
            }
            catch { }
        }

        /// <summary>Call when player connects. Returns the index of the new entry.</summary>
        public static int AddEntry(string routeFrom, string routeTo, string trainType, string trainNumber, string locomotive, int wagonCount)
        {
            var entries = Load();
            entries.Add(new HistoryEntry
            {
                Date         = DateTime.Now.ToString("dd.MM.yyyy"),
                ConnectedAt  = DateTime.Now.ToString("HH:mm"),
                RouteFrom    = routeFrom,
                RouteTo      = routeTo,
                TrainType    = trainType,
                TrainNumber  = trainNumber,
                Locomotive   = locomotive,
                WagonCount   = wagonCount,
            });
            Save(entries);
            return entries.Count - 1;
        }

        /// <summary>Call when player disconnects to fill in the end time.</summary>
        public static void CloseEntry(int index)
        {
            var entries = Load();
            if (index >= 0 && index < entries.Count)
            {
                entries[index].DisconnectedAt = DateTime.Now.ToString("HH:mm");
                Save(entries);
            }
        }
    }
}

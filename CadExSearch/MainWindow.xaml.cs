using CadExSearch.Commons;
using System;
using System.Collections;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

#pragma warning disable IDE0011

namespace CadExSearch
{
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            Client = new CadEx
            {
                SorterExpression = @"(?:\d+:){2}(?<s2>\d+):(?<s1>\d+)",
                FilterExpression = @"^[^#]+#\s[РУН]"
            };

            InitializeComponent();
        }

        public bool IsSearchByAddressEnabled { get; set; } = true;

        public CadEx Client { get; }

        public static ReAct SelectAll { get; } = ReAct.Do<ListBox>(lb =>
        {
            if (lb?.Items is null || lb.Items is {Count: 0}) return;
            if (lb.SelectedItems is {Count: 0} || lb.SelectedItems.Count != lb.Items.Count)
                lb.SelectAll();
            else
                lb.UnselectAll();
        }, lb => lb?.SelectedItems is not {Count: 0});

        public static ReAct UnSelectAll { get; } = ReAct.Do<ListBox>(lb =>
        {
            if (lb?.Items is null || lb.Items is {Count: 0}) return;
            lb.UnselectAll();
        }, lb => lb?.SelectedItems is not {Count: 0});

        public static ReAct Copy { get; } = ReAct.Do<IList>(list =>
        {
            if (list is {Count: 0}) return;
            var collected = string.Join("\r\n", list.Cast<CadExResult>().Select(r => r.DefaultView));
            if (!string.IsNullOrWhiteSpace(collected))
                Clipboard.SetText(collected);
        });

        public static ReAct CopyAsJson { get; } = ReAct.Do<IList>(list =>
        {
            if (list is {Count: 0}) return;
            try
            {
                var collected = JsonSerializer.Serialize(list.Cast<CadExResult>().ToArray(), new JsonSerializerOptions() {WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping});
                Clipboard.SetText(collected);
            }
            catch { /**/ }
        });

        public static ReAct OpenLink { get; } = ReAct.Do<string>(s =>
        {
            if (Uri.TryCreate(s, UriKind.Absolute, out _))
                Process.Start(new ProcessStartInfo("cmd", $"/c start {s}") { CreateNoWindow = true });
        }, s => Uri.TryCreate(s, UriKind.Absolute, out _));

        public bool AppendMode { get; set; }
        
        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            if (!AppendMode)
                Client.RawFetchedResults.Clear();

            if (IsSearchByAddressEnabled)
                await Client.DownloadResults(Street.Text, House.Text, Building.Text, Structure.Text, Apartment.Text);
            else
                await Client.DownloadResults(CadNum.Text);
            StatePb.Value = StatePb.Maximum;
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            SubjectId.SelectedIndex = -1;

            Street.Text = "";
            House.Text = "";
            Building.Text = "";
            Apartment.Text = "";
            Structure.Text = "";

            CadNum.Text = "";
        }


#pragma warning disable IDE1006 // Стили именования
        private void subject_id_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RegionId.SelectedIndex = -1;
            SettlementId.SelectedIndex = -1;
            SettlementType.SelectedIndex = -1;
            Client.SelectedSubject = e.AddedItems?.OfType<(string, string)>().FirstOrDefault() ?? default;
        }

        private void region_id_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SettlementId.SelectedIndex = -1;
            SettlementType.SelectedIndex = -1;
            Client.SelectedRegion = e.AddedItems?.OfType<(string, string)>().FirstOrDefault() ?? default;
        }

        private void settlement_type_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Client.SelectedSettlementType = e.AddedItems?.OfType<(string, string)>().FirstOrDefault() ?? default;
        }

        private void settlement_id_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Client.SelectedSettlement = e.AddedItems?.OfType<(string, string)>().FirstOrDefault() ?? default;
        }

        private void house_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                Button_Click(this, default);
        }

#pragma warning restore IDE1006 // Стили именования
        private void ButtonBase_OnClick2(object sender, RoutedEventArgs e)
        {
            Client.NewSession(true);
        }
    }
}
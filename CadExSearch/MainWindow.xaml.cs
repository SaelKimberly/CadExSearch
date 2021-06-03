using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using DynamicData.Binding;
using RestSharp;
using SaelSharp.Helpers;

namespace CadExSearch
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            Client = new CEOCC
            {
                EachResultModifier = Updater,
                SorterExpression = @"(?:\d+:){2}(?<s2>\d+):(?<s1>\d+)",
                FilterExpression = @"^[^#]+#\s[РУН]"
            };

            InitializeComponent();
        }

        public bool IsSearchByAddressEnabled { get; set; } = true;

        private CookieContainer Cookie { get; } = new();
        public CEOCC Client { get; }

        public static ReAct SelectAll { get; } = ReAct.Do<ListBox>(lb =>
        {
            if ((lb?.SelectedItems?.Count ?? 0) == 0 || (lb?.SelectedItems?.Count ?? 0) != (lb?.Items?.Count ?? 0))
                lb?.SelectAll();
            else
                lb?.UnselectAll();
        }, lb => (lb?.SelectedItems?.Count ?? 0) != 0);

        public static ReAct UnSelectAll { get; } = ReAct.Do<ListBox>(lb =>
        {
            if ((lb?.SelectedItems?.Count ?? 0) > 0)
                lb?.UnselectAll();
        }, lb => (lb?.SelectedItems?.Count ?? 0) != 0);

        public static ReAct Copy { get; } = ReAct.Do<IList>(list =>
        {
            if (list == default) return;
            var collected = string.Join("\r\n", list.Cast<CEOCCResult>().Select(r => r.DefaultView));
            if (!string.IsNullOrWhiteSpace(collected))
                Clipboard.SetText(collected);
        });

        public static ReAct OpenLink { get; } = ReAct.Do<string>(s =>
        {
            if (Uri.TryCreate(s, UriKind.Absolute, out _))
                Process.Start(new ProcessStartInfo("cmd", $"/c start {s}") { CreateNoWindow = true });
        }, s => Uri.TryCreate(s, UriKind.Absolute, out _));

        public bool AppendMode { get; set; } = false;
        public ObservableCollectionExtended<CEOCCResult> FetchedRecords { get; set; } = new();

        public CEOCCResult Updater(CEOCCResult r)
        {
            try
            {
                if (r.PortalAddress == default || Client.UseResultModifyerIfExists == null)
                    return r with { Status = "Неизвестный" };
                var mode = Client.UseResultModifyerIfExists == true;
                var subClient =
                    new RestClient(mode ? "http://rosreestr.gov.ru/api/online/" : "https://rosreestr.gov.ru/wps/")
                    {
                        Timeout = -1,
                        CookieContainer = mode ? Cookie : Client.Cookie,
                        UserAgent = CEOCC.UserAgent
                    };


                //Remove leading zero's from CN.
                var cn = r.CadNumber;
                cn = Regex.Replace(cn, @":0+", ":");
                cn = Regex.Replace(cn, @"^0+", "");
                cn = Regex.Replace(cn, @"::", ":0:");
                cn = Regex.Replace(cn, @":$", ":0");
                cn = Regex.Replace(cn, @"^:", "0:");

                IRestResponse res = default;
                for (var i = 0; i < 5; i++)
                {
                    res = subClient.Execute(new RestRequest(mode ? $"/fir_object/{cn}" : r.PortalAddress));
                    if (res.StatusCode == HttpStatusCode.OK || res.StatusCode == HttpStatusCode.NoContent)
                        break;
                }

                if (res is not { StatusCode: HttpStatusCode.OK }) return r with { Status = "Неизвестный" };
                if (!mode)
                {
                    var extended = new Dictionary<string, string>(
                        from m in Regex.Matches(res.Content,
                            @"<tr>\s*<td[^>]+>\s*(<(\w+)>)?(?<key>[^<]+)(</\2>)?\s*</td>\s*<td[^>]+>\s*<b>(?<val>[^<]+)")
                        let key = m.Groups["key"].Value.Trim().Replace("&nbsp;", " ")
                        let val = m.Groups["val"].Value.Trim().Replace("&nbsp;", " ")
                        select new KeyValuePair<string, string>(key, val));
                    var status =
                        (from kvp in extended where Regex.IsMatch(kvp.Key, "[Сс]татус") select kvp.Value)
                        .FirstOrDefault() ?? "Неизвестный";
                    var pkk5 = Regex.Match(res.Content, @"href=""(?<pkk5>.+pkk5[^""]+).+Най")?.Groups["pkk5"]?.Value;
                    try
                    {
                        var right = Regex.Replace(
                            Regex.Match(res.Content, @"<td.+width=""35%"">(?<right>[^<]+)").Groups["right"].Value,
                            @"(\n)|(\s{2,})|(&nbsp;)", " ");
                        if (!string.IsNullOrWhiteSpace(right))
                            extended.Add("Право:", right);
                    }
                    catch
                    {
                        // ignored
                    }

                    try
                    {
                        var locks = Regex.Replace(
                            Regex.Match(res.Content, @"<td.+width=""65%"">(?<right>[^<]+)").Groups["right"].Value,
                            @"(\n)|(\s{2,})|(&nbsp;)", " ");
                        if (!string.IsNullOrWhiteSpace(locks))
                            extended.Add("Ограничение:", locks);
                    }
                    catch
                    {
                        // ignored
                    }

                    return r with { Status = status, Extended = extended, PKK5Address = pkk5 };
                }

                var st = Regex.Match(res.Content, @"tusStr"":""(?<state>[^""]+)")?.Groups["state"]?.Value; // Status
                var ad = Regex.Match(res.Content, @"fir\w+"":""(?<state>[^""]+)")?.Groups["state"]
                    ?.Value; // FirActualDate
                return r with
                {
                    Status = string.IsNullOrWhiteSpace(st) ? "Неизвестен" : st,
                    Extended = new Dictionary<string, string> { { "Актуально (ФИР):", ad } }
                };
            }
            catch
            {
                return r with { Status = "Неизвестный" };
            }
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            if (!AppendMode)
                Client.RawFetchedResults.Clear();

            if (IsSearchByAddressEnabled)
                await Client.DownloadResults(street.Text, house.Text, building.Text, structure.Text, apartment.Text);
            else
                foreach (var input in ParseCadasters(cad_num.Text))
                    await Client.DownloadResults(input);
            state_pb.Value = state_pb.Maximum;
        }

        private IEnumerable<string> ParseCadasters(string cads)
        {
            var cad = cads.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => Regex.Replace(s.Trim(), @"\s+", ""));
            var ret = new List<string>(20);
            foreach (var c in cad)
                if (c.Contains('-'))
                {
                    var parts = c.Split(':');
                    if (!parts[3].Contains('-')) continue;
                    var pp = parts[3].Split('-');

                    if (!int.TryParse(pp[0], out var s) || !int.TryParse(pp[1], out var e) || s >= e) yield break;

                    for (var j = s; j < e; j++)
                    {
                        var _r = string.Join(':', parts[0], parts[1], parts[2], j.ToString());
                        ret.Add(_r);
                        if (ret.Count != 20) continue;
                        yield return string.Join(';', ret);
                        ret.Clear();
                    }
                }
                else
                {
                    ret.Add(c);
                    if (ret.Count != 20) continue;
                    yield return string.Join(';', ret);
                    ret.Clear();
                }

            if (ret.Any())
                yield return string.Join(';', ret);
        }


#pragma warning disable IDE1006 // Стили именования
        private void subject_id_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            region_id.SelectedIndex = -1;
            settlement_id.SelectedIndex = -1;
            settlement_type.SelectedIndex = -1;
            Client.SelectedSubject = e.AddedItems?.OfType<(string, string)>().FirstOrDefault() ?? default;
        }

        private void region_id_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            settlement_id.SelectedIndex = -1;
            settlement_type.SelectedIndex = -1;
            Client.SelectedRegion = e.AddedItems?.OfType<(string, string)>().FirstOrDefault() ?? default;
        }

        private void settlement_type_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Client.SelectedSettlementType = e.AddedItems?.OfType<(string, string)>().FirstOrDefault() ?? default;
        }

        private void captchaText_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                Button_Click(sender, default);
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
        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            Client.NewSession(true);
        }
    }
}


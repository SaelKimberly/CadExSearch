using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using CadExSearch.Commons;
using DynamicData.Binding;
using Medallion.Threading.FileSystem;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using RestSharp;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reactive.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Data;

#pragma warning disable IDE0011
#pragma warning disable IDE0055
#pragma warning disable CA1707
#pragma warning disable CA1805
#pragma warning disable CA2201

// ReSharper disable StringLiteralTypo

namespace CadExSearch
{
    // ReSharper disable once InconsistentNaming
    public delegate CadExResult OnSingleResult(CadExResult cadn_desc_addr);

    public record CadExResult
    {
        public string CadNumber { get; set; }
        public string Address { get; set; }
        [JsonIgnore]
        public string PortalAddress { get; set; }

        public Dictionary<string, string> Extended { get; set; } = default;
        // ReSharper disable once InconsistentNaming
        public string PKK5Address { get; set; } = default;
        public string Status { get; set; } = default;
        [JsonIgnore]
        public string DefaultView => $"{CadNumber}\t# {Status ?? "Неизвестный"}\t# {Address}";
    }


    // ReSharper disable InconsistentNaming
    // ReSharper disable IdentifierTypo
    public class CadEx : ReactiveObject
    {
        static CadEx()
        {
            using var context = new CacheContext();
            using (GlobalLock.Acquire())
            {
                if (!context.Database.EnsureCreated() || context.Records.Any()) return;
                context.Records.Add(new Record { Id = "subject", BaseId = "subject", Content = "root_record" });
                context.SaveChanges();
            }
        }

        private object itemsLock;
        private bool isFullyInitialized;
        public CadEx()
        {
            Client = new RestClient("https://rosreestr.gov.ru/wps/")
            {
                CookieContainer = Cookie,
                Timeout = -1,
                UserAgent = UserAgent
            };
            Client.AddDefaultHeaders(new Dictionary<string, string>
            {
                {"Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8"},
                {"Accept-Language", "ru-RU,ru;q=0.8,en-US;q=0.5,en;q=0.3"},
                {"Connection", "keep-alive"}
            });
            EachResultModifier = (r) => DefaultResultModifier(r, UseResultModifyerIfExists);

            this.WhenAnyValue(_ => _.Message)
                .Where(_ => _ != "")
                .Delay(TimeSpan.FromSeconds(5))
                .Subscribe(_ => Message = "");

            this.WhenAnyValue(_ => _.InitialPage)
                .Subscribe(async _ =>
                {
                    if (isFullyInitialized) goto jpl; 

                    RawFetchedResults = new ObservableCollectionExtended<CadExResult>();

                    Subjects = new ObservableCollectionExtended<(string, string)>();
                    Regions = new ObservableCollectionExtended<(string, string)>();
                    Settlement = new ObservableCollectionExtended<(string, string)>();
                    SettlementTypes = new ObservableCollectionExtended<(string, string)>();
                    StreetTypes = new ObservableCollectionExtended<(string, string)>();

                    itemsLock = new object();
                    
                    BindingOperations.EnableCollectionSynchronization(Subjects, itemsLock);
                    BindingOperations.EnableCollectionSynchronization(Regions, itemsLock);
                    BindingOperations.EnableCollectionSynchronization(Settlement, itemsLock);
                    BindingOperations.EnableCollectionSynchronization(SettlementTypes, itemsLock);
                    BindingOperations.EnableCollectionSynchronization(StreetTypes, itemsLock);

                    FetchedResults = (ListCollectionView)CollectionViewSource.GetDefaultView(RawFetchedResults);
                    FetchedResults.Filter = s =>
                    {
                        var r = s as CadExResult;
                        if (string.IsNullOrWhiteSpace(FilterExpression) || FilterDirection == null) return true;
                        try
                        {
                            return Regex.IsMatch(r?.DefaultView ?? string.Empty, FilterExpression) == FilterDirection;
                        }
                        catch
                        {
                            return true;
                        }
                    };

                    RawFetchedResults.CollectionChanged += (_, _) =>
                    {
                        TotalFetch = RawFetchedResults.Count;
                        TotalShown = FetchedResults.Count;
                    }; 
                    jpl:
                    isFullyInitialized = true;
                    await PreloadCommonAssets();
                });

            this.WhenAnyValue(_ => _.SelectedSubject)
                .Subscribe(async s =>
                {
                    await UpdateBySubject(s);
                    RegionAvailable = s != default;
                });

            
            
            this.WhenAnyValue(_ => _.SelectedRegion)
                .Subscribe(async s =>
                {
                    await UpdateByRegion(s);
                    SettlementAvailable = s != default;
                    SettlementTypeAvailable = s != default;
                });

            this.WhenAnyValue(_ => _.SelectedSettlementType)
                .Subscribe(async s =>
                {
                    await UpdateBySettlementType(s);
                    SettlementAvailable = s != default;
                });

            this.WhenAnyValue(_ => _.SorterExpression, _ => _.SortDirection)
                .Throttle(TimeSpan.FromSeconds(1))
                .Select(_ => RefreshSorter())
                .ObserveOnDispatcher()
                .Subscribe(_ =>
                {
                    FetchedResults.CustomSort = _;
                    FetchedResults.Refresh();
                });

            this.WhenAnyValue(_ => _.FilterExpression, _ => _.FilterDirection)
                .Throttle(TimeSpan.FromSeconds(1))
                .ObserveOnDispatcher()
                .Subscribe(_ => FetchedResults.Refresh());

            InitialConnect();
        }

        [Reactive] public (string, string) SelectedSubject { get; set; }
        [Reactive] public (string, string) SelectedRegion { get; set; }
        [Reactive] public (string, string) SelectedSettlementType { get; set; }
        [Reactive] public (string, string) SelectedSettlement { get; set; }
        [Reactive] public (string, string) SelectedStreetType { get; set; }

        [Reactive] public bool RegionAvailable { get; private set; }
        [Reactive] public bool SettlementTypeAvailable { get; private set; }
        [Reactive] public bool SettlementAvailable { get; private set; }

        [Reactive] public bool IsConnected { get; private set; }
        [Reactive] public string Message { get; private set; } = "";

        public ObservableCollectionExtended<(string, string)> Subjects { get; private set; }
        public ObservableCollectionExtended<(string, string)> Regions { get; private set; }
        public ObservableCollectionExtended<(string, string)> SettlementTypes { get; private set; }
        public ObservableCollectionExtended<(string, string)> Settlement { get; private set; }
        public ObservableCollectionExtended<(string, string)> StreetTypes { get; private set; }

        public ObservableCollectionExtended<CadExResult> RawFetchedResults { get; private set; }
        public ListCollectionView FetchedResults { get; private set; }

        [Reactive] public string FilterExpression { get; set; }
        [Reactive] public bool? FilterDirection { get; set; } = true;

        [Reactive] public string SorterExpression { get; set; }
        [Reactive] public bool? SortDirection { get; set; } = false;

        [Reactive] public int TotalFound { get; private set; }
        [Reactive] public int TotalFetch { get; private set; }
        [Reactive] public int TotalShown { get; private set; }
        [Reactive] public int TotalTime { get; private set; }

        [Reactive] public bool IsBusy { get; private set; }
        [Reactive] public bool? UseResultModifyerIfExists { get; set; } = true;
        public OnSingleResult EachResultModifier { get; set; }

        public static CadExResult DefaultResultModifier(CadExResult r, bool? usage)
        {
            try
            {
                if (r.PortalAddress == default || usage == null)
                    return r with { Status = "Неизвестный" };
                var mode = usage == true;
                var subClient =
                    new RestClient(mode ? "http://rosreestr.gov.ru/api/online/" : "https://rosreestr.gov.ru/wps/")
                    {
                        Timeout = -1,
                        CookieContainer = mode ? Cookie : CadEx.Cookie,
                        UserAgent = CadEx.UserAgent
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
                    if (res is { StatusCode: HttpStatusCode.OK } or { StatusCode: HttpStatusCode.NoContent })
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
                var ad = Regex.Match(res.Content, @"fir\w+"":""(?<state>[^""]+)")?.Groups["state"]?.Value; // FirActualDate
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

        private async void InitialConnect()
        {
            if (IsConnected) return;
            while (!IsConnected)
                try
                {
                    var response = await Client.ExecuteAsync(
                        new RestRequest("/portal/p/cc_ib_portal_services/online_request", Method.GET)
                            .AddHeader("Upgrade-Insecure-Requests", "1")
                            .AddHeader("Cache-Control", "max-age=0"));

                    if (response.StatusCode != HttpStatusCode.OK)
                        throw new ApplicationException("Cannot fetch page from RR!");

                    if (!response.Content.TryMatch(
                        @"<input type=""radio"" name=""search_type"" value=""ADDRESS"".+>", out _))
                        throw new ApplicationException("Fetched page has invalid structure!");

                    InitialPage = response.Content;

                    URIs["session-addr"] = response.Content.TryMatch(
                        @"<base href=""https://rosreestr.gov.ru/wps(?<haddr>[^""]+)", _ => _.Groups["haddr"].Value);

                    URIs["captcha-addr"] = URIs["session-addr"] + response.Content.TryMatch(
                        @"#captchaImage2'\)\.attr\('src',\s'(?<haddr>[^']+)", _ => _.Groups["haddr"].Value);

                    URIs["form-addr"] = URIs["session-addr"] + response.Content.TryMatch(
                        @"<form action=""(?<haddr>[^""]+)", _ => _.Groups["haddr"].Value);

                    IsConnected = true;
                }
                catch (Exception e)
                {
                    Message = e.Message + "\nНе удалось подключиться. Повторная попытка через 5 секунд...";
                    await Task.Delay(new TimeSpan(0, 0, 5));
                }
        }

        private async Task PreloadCommonAssets()
        {
            IsBusy = true;
            bool subs_no;
            bool strs_no;
            if (itemsLock is null)
            {
                IsBusy = false;
                return;
            }
            lock (itemsLock)
                if (Subjects is null || StreetTypes is null || InitialPage is null)
                {
                    IsBusy = false;
                    return;
                }

            lock (itemsLock)
            {
                subs_no = !Subjects.Any();
                strs_no = !StreetTypes.Any();
            }

            if (subs_no || strs_no)
            {
                var subs = CacheGetValues("subject");
                var strs = CacheGetValues("STR");
                
                using var doc = await Parser.ParseDocumentAsync(InitialPage);
                if (!(subs?.Any() ?? false) && subs_no)
                {
                    subs = (from s in doc.QuerySelectorAll("select#oSubjectId > option").Skip(1)
                        select (s.GetAttribute("value"), s.InnerHtml)).ToArray();
                    
                    CacheSetValues("subject", subs);
                }

                if (!(strs?.Any() ?? false) && !strs_no)
                {
                    strs = (from s in doc.QuerySelectorAll("select[name=street_type] > option").Skip(1)
                        select (s.GetAttribute("value"), s.InnerHtml)).ToArray();
                    await AddFork("subject", "STR");
                    CacheSetValues("STR", strs);
                }
                if (subs_no) lock (itemsLock) Subjects.AddRange(subs);
                if (strs_no) lock (itemsLock) StreetTypes.AddRange(strs ?? Array.Empty<(string, string)>());
            }

            IsBusy = false;
        }

        private async Task UpdateBySubject((string, string) subject)
        {
            IsBusy = true;
            if (subject == default) goto lbl;
            var (id, _) = subject;
            var regs = CacheGetValues($"SUB:{id}");
            if (regs == null || !regs.Any())
            {
                var response = await Client.ExecuteAsync(
                    new RestRequest($"/PA_RRORSrviceExtended/Servlet/ChildsRegionController?parentId={id}",
                            Method.GET)
                        .AddHeader("X-Requested-With", "XMLHttpRequest")
                        .AddHeader("X-Prototype-Version", "1.5.0_rc2")
                        .AddHeader("Connection", "keep-alive")
                        .AddHeader("Referer", Referer)
                        .AddHeader("Cache-Control", "max-age=0"));

                regs = response.Content.TryMatches(@"(?<code>\d+);(?<name>.+)",
                    m => (m.Groups["code"].Value, m.Groups["name"].Value)).ToArray();

                await AddFork("subject", $"SUB:{id}");
                CacheSetValues($"SUB:{id}", regs);
            }

            lock (itemsLock)
            {
                Regions.Clear();
                Regions.AddRange(regs);
            }
            
            lbl:
            SelectedRegion = default;
            SelectedSettlement = default;
            SelectedSettlementType = default;
            IsBusy = false;
        }

        private async Task UpdateByRegion((string, string) region)
        {
            IsBusy = true;
            if (region == default) goto lbl;
            var (id, _) = region;

            var setm = CacheGetValues($"REG:S:{id}");
            var sett = CacheGetValues($"REG:T:{id}");
            if (setm == null || !setm.Any())
            {
                var response = await Client.ExecuteAsync(
                    new RestRequest(
                            $"/PA_RRORSrviceExtended/Servlet/ChildsRegionController?parentId={id}&settlement_type=-1&add_settlement_type=true",
                            Method.GET)
                        .AddHeader("X-Requested-With", "XMLHttpRequest")
                        .AddHeader("X-Prototype-Version", "1.5.0_rc2")
                        .AddHeader("Connection", "keep-alive")
                        .AddHeader("Referer", Referer)
                        .AddHeader("Cache-Control", "max-age=0"));

                setm = response.Content.TryMatches(@"(?<code>\d+);(?<name>.+)",
                    m => (m.Groups["code"].Value, m.Groups["name"].Value)).ToArray();
                await AddFork("subject", $"REG:S:{id}");
                CacheSetValues($"REG:S:{id}", setm);
            }

            lock (itemsLock)
            {
                Settlement.Clear();
                Settlement.AddRange(setm);
            }

            if (sett == null || !sett.Any())
            {
                var response = await Client.ExecuteAsync(
                    new RestRequest(
                            $"/PA_RRORSrviceExtended/Servlet/ChildsRegionTypesController?parentId={id}",
                            Method.GET)
                        .AddHeader("X-Requested-With", "XMLHttpRequest")
                        .AddHeader("X-Prototype-Version", "1.5.0_rc2")
                        .AddHeader("Connection", "keep-alive")
                        .AddHeader("Referer", Referer)
                        .AddHeader("Cache-Control", "max-age=0"));

                sett = response.Content.TryMatches(@"(?<code>\d+);(?<name>.+)",
                    m => (m.Groups["code"].Value, m.Groups["name"].Value)).ToArray();

                await AddFork("subject", $"REG:T:{id}");
                CacheSetValues($"REG:T:{id}", sett);
            }

            lock (itemsLock)
            {
                SettlementTypes.Clear();
                SettlementTypes.AddRange(sett);
            }

            lbl:
            SelectedSettlement = default;
            SelectedSettlementType = default;
            IsBusy = false;
        }

        private async Task UpdateBySettlementType((string, string) type)
        {
            IsBusy = true;
            if (SelectedRegion == default || type == default) goto lbl;

            var (id, _) = type;
            var set = CacheGetValues($"REG:{SelectedRegion}:S:{id}");
            if (set == null || !set.Any())
            {
                var response = await Client.ExecuteAsync(
                    new RestRequest(
                            $"/PA_RRORSrviceExtended/Servlet/ChildsRegionController?parentId={SelectedRegion}&settlement_type={id}&add_settlement_type=true",
                            Method.GET)
                        .AddHeader("X-Requested-With", "XMLHttpRequest")
                        .AddHeader("X-Prototype-Version", "1.5.0_rc2")
                        .AddHeader("Connection", "keep-alive")
                        .AddHeader("Referer", Referer)
                        .AddHeader("Cache-Control", "max-age=0"));

                set = response.Content.TryMatches(@"(?<code>\d+);(?<name>.+)",
                    m => (m.Groups["code"].Value, m.Groups["name"].Value)).ToArray();

                await AddFork("subject", $"REG:{SelectedRegion}:S:{id}");
                CacheSetValues($"REG:{SelectedRegion}:S:{id}", Settlement);
            }

            lock (itemsLock)
            {
                Settlement.Clear();
                Settlement.AddRange(set);
            }

            lbl:
            SelectedSettlement = default;
            IsBusy = false;
        }

        private IComparer RefreshSorter()
        {
            return SimpleComparer<CadExResult>.Of((r_s1, r_s2) =>
            {
                if (SorterExpression.IsNullOrWhitespace()) return 0;

                var s1 = r_s1.DefaultView;
                var s2 = r_s2.DefaultView;

                if (!SorterExpression.TryToRegex(out var reg)) return 0;
                var sorts = from m in Regex.Matches(SorterExpression, @"\(\?\<(?<sort>s\d+)\>")
                    let ret = m.Groups["sort"].Value
                    let wgt = int.Parse(Regex.Match(ret, @"(?<num>\d+)").Groups["num"].Value, CultureInfo.InvariantCulture.NumberFormat)
                    orderby wgt
                    select (ret, wgt);
                bool im1; try { im1 = reg.IsMatch(s1); } catch { im1 = false; }
                bool im2; try { im2 = reg.IsMatch(s2); } catch { im2 = false; }

                if (!im1 || !im2)
                    return (im1, im2) switch {(true, false) => 1, (false, true) => -1, _ => 0};

                var cmp = StringComparer.FromComparison(StringComparison.OrdinalIgnoreCase);
                var ms = 0;
#pragma warning disable IDE0072
                foreach (var (ret, wgt) in sorts)
                    ms += (reg.Match(s1).Groups[ret].Value, reg.Match(s2).Groups[ret].Value) switch
                    {
                        var (m1, m2) when int.TryParse(m1, out var m1d) && int.TryParse(m2, out var m2d) =>
                            (m1d > m2d ? 1 : m1d == m2d ? 0 : -1) * wgt,
                        var (m1, m2) when m1.Length != m2.Length =>
                            (m1.Length > m2.Length ? 1 : -1) * wgt,
                        var (m1, m2) =>
                            cmp.Compare(m1, m2) * wgt
                    };
#pragma warning restore IDE0072
                return ms switch {< 0 => -1, > 0 => 1, _ => 0};
            }, SortDirection);
        }

        private async Task<(bool, IHtmlDocument)> PrepareByPage(string content, int preMax = -1)
        {
            #region Preparing For Parsing. Getting Messages & Errors

            TotalFound = RawFetchedResults.Count;
            var page = await Parser.ParseDocumentAsync(content);
            //Find information and error messages on the first results page (or main page, if failed).
            var messages = page.QuerySelectorAll("td.infomsg1 span.t12");
            if (messages.Any())
            {
                var msg = Regex.Replace(messages.First().InnerHtml,
                    @"(^\s*)|(\s*$)|(&nbsp;)|(\n)|(\t)|(<[^>]+>)", "");
                var failed = !msg.StartsWith("Найдено", StringComparison.InvariantCulture);
                Message = msg;
                if (failed) return (false, default);
            }

            // Results count parsing from first results page.
            var page_stats = page.QuerySelectorAll("div#pg_stats");
            if (!page_stats.Any())
            {
                Message = "Не удалось обработать страницу!";
                return (false, default);
            }

            TotalFound += preMax != -1 ? preMax :
                page_stats.First().InnerHtml.TryMatch(@"\D*(?<all>\d+)", out var m) ? 
                    m.Select("all", int.Parse) : 0;

            return (true, page);

            #endregion
        }

        private IEnumerable<CadExResult> ParsePage(IParentNode page)
        {
            var erm = UseResultModifyerIfExists != null ? EachResultModifier ?? (s => s) : s => s;

            return from node in page.QuerySelectorAll("tr[id^=js_oTr]")
                let cn = node.QuerySelector("td:nth-child(2)").InnerHtml
                let cadn = string.IsNullOrWhiteSpace(cn)
                    ? ""
                    : Regex.Match(cn, ">(?<cn>[^<]+)<").Groups["cn"].Value
                let addr =
                    $"{URIs["session-addr"]}{node.QuerySelector("td:nth-child(1) > a").GetAttribute("href")}"
                let desc = Regex.Replace(
                    Regex.Replace(node.QuerySelector("td:nth-child(1) > a").InnerHtml, @"(\n)|(\s{2,})", " "),
                    @"&[^;]+;", "")
                where cadn != ""
                select erm(new CadExResult
                    {
                        CadNumber = Regex.Replace(cadn, @"(^\s*)|(\s*$)", ""),
                        Address = Regex.Replace(desc, @"(^\s*)|(\s*$)", ""),
                        PortalAddress = addr
                    });
        }

        // ReSharper disable once MethodOverloadWithOptionalParameter
        private IRestRequest AddHeaders(IRestRequest request, string street, string house = "", string building = "",
            string structure = "", string apartment = "")
        {
            return request.AddHeader("Content-Type", "application/x-www-form-urlencoded")
                .AddHeader("Origin", "https://rosreestr.gov.ru")
                .AddHeader("Referer", "https://rosreestr.gov.ru/wps/portal/online_request")
                .AddHeader("Upgrade-Insecure-Requests", "1")
                .AddParameter("search_action", "true")
                //SCRAM
                .AddParameter("subject", "")
                .AddParameter("region", "")
                .AddParameter("settlement", SelectedSettlement.Item1)
                .AddParameter("cad_num", "")
                .AddParameter("start_position", "59")
                .AddParameter("obj_num", "")
                .AddParameter("old_number", "")

                //FILLED FIELDS
                .AddParameter("search_type", "ADDRESS")
                .AddParameter("subject_id", SelectedSubject.Item1)
                .AddParameter("region_id", SelectedRegion.Item1)
                .AddParameter("settlement_id", SelectedSettlement.Item1)
                .AddParameter("settlement_type",
                    SelectedSettlementType == default ? "-1" : SelectedSettlementType.Item1)
                .AddParameter("street_type",
                    SelectedStreetType == default ? "str0" : SelectedStreetType.Item1)
                .AddParameter("street", Regex.Replace(street, @"(^\s*)|(\s*$)", ""))
                .AddParameter("house", Regex.Replace(house, @"(^\s*)|(\s*$)", ""))
                .AddParameter("building", Regex.Replace(building, @"(^\s*)|(\s*$)", ""))
                .AddParameter("structure", Regex.Replace(structure, @"(^\s*)|(\s*$)", ""))
                .AddParameter("apartment", Regex.Replace(apartment, @"(^\s*)|(\s*$)", ""))
                //SCRAM
                .AddParameter("right_reg", "")
                .AddParameter("encumbrance_reg", "");
        }

        private IRestRequest AddHeaders(IRestRequest request, string cad_number)
        {
            return request.AddHeader("Content-Type", "application/x-www-form-urlencoded")
                .AddHeader("Origin", "https://rosreestr.gov.ru")
                .AddHeader("Referer", "https://rosreestr.gov.ru/wps/portal/online_request")
                .AddHeader("Upgrade-Insecure-Requests", "1")
                .AddParameter("search_action", "true")
                //SCRAM
                .AddParameter("subject", "")
                .AddParameter("region", "")
                .AddParameter("settlement", SelectedSettlement.Item1 ?? "")
                .AddParameter("start_position", "59")
                .AddParameter("obj_num", "")
                .AddParameter("old_number", "")
                //.AddParameter("o_subject_id", "110000000000")
                .AddParameter("subject_id", "")
                .AddParameter("region_id", "")
                .AddParameter("settlement_id", "")
                .AddParameter("settlement_type", "-1")
                .AddParameter("street_type", "str0")
                .AddParameter("street", "")
                .AddParameter("house", "")
                .AddParameter("building", "")
                .AddParameter("structure", "")
                .AddParameter("apartment", "")
                //FILLED FIELDS
                .AddParameter("search_type", "CAD_NUMBER")
                .AddParameter("cad_num", cad_number)

                //SCRAM
                .AddParameter("right_reg", "")
                .AddParameter("encumbrance_reg", "");

            //.AddParameter("captchaText", EnteredCaptcha);
        }

        private async Task<bool> StartFetching(IRestRequest initialRequest, int preMax = -1)
        {
            #region Stage1 : Prepare

            var sw = new Stopwatch();
            sw.Start();

            int GetCurTime() => (int)sw.Elapsed.TotalSeconds;

            var response = await Client.ExecuteAsync(initialRequest);
            if (response is not {StatusCode: HttpStatusCode.OK})
            {
                Message = "Не удалось получить данные по вашему запросу!";
                return false;
            }

            var (success, page) = await PrepareByPage(response.Content, preMax);
            if (!success) return false;

            var erm = UseResultModifyerIfExists != null ? EachResultModifier ?? (s => s) : s => s;

            #endregion

            #region Current Page Parsing - LINQ Mode

            using (var worker = new BackgroundWorker())
            {
                worker.Connect(new CadExResult[] {default}, r =>
                        ParsePage(page).Select(_ =>
                        {
                            TotalTime = GetCurTime();
                            return erm(_);
                        }).ToArray(), 
                    RawFetchedResults);
                worker.RunWorkerAsync();
                await worker.WaitForComplete();
            }

            #endregion

            if (TotalFound <= 20) return true;

            var portlet_id = page.QuerySelector(".asa\\.portlet\\.id").InnerHtml;

            using (var worker = new BackgroundWorker())
            {
                worker.Connect(
                    Enumerable.Range(0, TotalFound / 20 + (TotalFound % 20 > 0 ? 1 : 0) - 1),
                    i =>
                    {
                        IRestResponse r1 = null;

                        for (var j = 0; j < 5 && r1 is not {StatusCode: HttpStatusCode.OK}; j++)
                            r1 = new RestClient("https://rosreestr.gov.ru/wps") {CookieContainer = Cookie}
                                .Execute(new RestRequest(
                                        $"{URIs["form-addr"]}?online_request_search_page={i + 2}#{portlet_id}")
                                    .AddHeader("Referer", Referer).AddHeader("Upgrade-Insecure-Requests", "1"));

                        TotalTime = GetCurTime();

                        var r2 = r1 is {StatusCode: HttpStatusCode.OK} ? Parser.ParseDocument(r1.Content) : null;
                        if (r2 is null) Message = $"Не удалось получить страницу {i}";

                        TotalTime = GetCurTime();

                        var r3 = r2 is null ? Array.Empty<CadExResult>() : ParsePage(r2).Select(r => erm(r)).ToArray();

                        return r3;
                    }, RawFetchedResults);
                worker.RunWorkerAsync();
                await worker.WaitForComplete();
            }

            TotalTime = GetCurTime();
            sw.Stop();
            return true;
        }

        public async Task<bool> DownloadResults(string street, string house, string building = "",
            string structure = "",
            string apartment = "")
        {
            if (IsBusy) return false;
            IsBusy = true;
            var initialRequest = AddHeaders(new RestRequest(URIs["form-addr"], Method.POST), street, house,
                building,
                structure, apartment);
            return await StartFetching(initialRequest).ContinueWith(_ => IsBusy = false);
        }

        public async Task<bool> DownloadResults(string cad_number)
        {
            if (IsBusy) return false;
            IsBusy = true;
            var prep = ParseCadasters(cad_number, out var count).ToList();
            if (prep.Any() && count > 0)
                return await Task.WhenAll(prep
                        .Select(async (c) =>
                            await StartFetching(AddHeaders(new RestRequest(URIs["form-addr"], Method.POST), c), count)))
                    .ContinueWith(_ => IsBusy = false);
            return false;
        }

        public void NewSession(bool clear)
        {
            if (clear)
            {
                RawFetchedResults.Clear();
                TotalFound = 0;
            }

            SelectedSubject = default;
            TotalTime = 0;
        }

        private static IEnumerable<string> ParseCadasters(string cads, out int count)
        {
            count = 0; var cadasters = new List<string>();

            var cad = cads.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => Regex.Replace(s.Trim(), @"\s+", ""));
            var ret = new List<string>(20);
            
            foreach (var c in cad)
                if (c.Contains('-'))
                {
                    var parts = c.Split(':');
                    if (!parts[3].Contains('-')) continue;
                    var pp = parts[3].Split('-');

                    if (!int.TryParse(pp[0], out var s) || !int.TryParse(pp[1], out var e) || s >= e) return cadasters;

                    for (var j = s; j < e; j++)
                    {
                        var _r = string.Join(':', parts[0], parts[1], parts[2], j.ToString(CultureInfo.InvariantCulture.NumberFormat));
                        ret.Add(_r);
                        if (ret.Count != 20) continue;
                        cadasters.Add(string.Join(';', ret));
                        count += ret.Count;
                        ret.Clear();
                    }
                }
                else
                {
                    ret.Add(c);
                    if (ret.Count != 20) continue;
                    cadasters.Add(string.Join(';', ret));
                    count += ret.Count;
                    ret.Clear();
                }

            if (!ret.Any()) return cadasters;

            cadasters.Add(string.Join(';', ret));
            count += ret.Count;

            return cadasters;
        }

        #region Cache

        //private record CacheRecord(string Id, (string, string) Value, string Type, string BaseId);

        private static FileDistributedLock GlobalLock { get; } = new (new FileInfo("CadEx.Cache.lock"));
        public static SHA512 SHA { get; } = SHA512.Create();

        public static string GetId((string,string) record)
        {
            var ms = new MemoryStream(Encoding.UTF8.GetBytes($"{record.Item1}:{record.Item2}"));
            var hash = Convert.ToBase64String(SHA.ComputeHash(ms));
            return Regex.Replace(hash, @"(\W|\d)", "").ToUpper(CultureInfo.InvariantCulture)[..20];
        }

        private static (string, string)[] CacheGetValues(string root)
        {
            using var cache = new CacheContext();
            using (GlobalLock.Acquire())
            {
                var ret = new List<(string, string)>();
                foreach (var rr in from r in cache.Records
                    where r.BaseId == root && r.Content != "root_record" && r.Content != "fork_record"
                    select r.Content.Split(':', StringSplitOptions.RemoveEmptyEntries))
                    ret.Add((rr[0], rr[1]));
                return ret.OrderBy(v => v.Item2).ToArray();
            }
        }

        private static async void CacheSetValues(string root, IEnumerable<(string, string)> values)
        {
            var vals = values as (string, string)[] ?? values.ToArray();
            if (!vals.Any()) return;
            await using (await GlobalLock.AcquireAsync())
            await using (var cache = new CacheContext())
            {
                foreach (var p in vals)
                {
                    var id = GetId(p);
                    if (cache.Records.Any(r => r.Id == id)) continue;
                    await cache.AddAsync(new Record {BaseId = root, Id = id, Content = $"{p.Item1}:{p.Item2}"});
                }
                await cache.SaveChangesAsync();
            }
        }

        private static async Task AddFork(string root, string fork)
        {
            try
            {
                await using (await GlobalLock.AcquireAsync())
                await using (var cache = new CacheContext())
                {
                    await cache.AddAsync(new Record { BaseId = root, Id = fork, Content = "fork_record" });
                    await cache.SaveChangesAsync();
                }
            }
            catch
            {
                // Ignored
            }
        }

        #endregion

        #region Internal Fields

        public static CookieContainer Cookie { get; } = new();

        public static string UserAgent { get; } =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:87.0) Gecko/20100101 Firefox/87.0";

        private RestClient Client { get; }
        private HtmlParser Parser { get; } = new();

        private Dictionary<string, string> URIs { get; } = new();
        [Reactive] private string InitialPage { get; set; }

        private string Referer { get; } = "https://rosreestr.gov.ru/wps/portal/p/cc_ib_portal_services/online_request";

        #endregion
    }
}
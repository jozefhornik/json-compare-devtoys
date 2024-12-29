using DevToys.Api;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;
using static DevToys.Api.GUI;

[assembly:System.Resources.NeutralResourcesLanguageAttribute("en-US")]

namespace JsonCompareDevToy
{
    [Export(typeof(IGuiTool))]
    [Name("JsonCompare")]
    [ToolDisplayInformation(
    IconFontName = "FluentSystemIcons",                                       
    IconGlyph = '\uE1D7',                                             
    GroupName = PredefinedCommonToolGroupNames.Text,                  
    ResourceManagerAssemblyIdentifier = nameof(JsonCompareResourceAssemblyIdentifier),
    ResourceManagerBaseName = "JsonCompareDevToy.Resources",                  
    ShortDisplayTitleResourceName = nameof(Resources.ShortDisplayTitle),   
    LongDisplayTitleResourceName = nameof(Resources.LongDisplayTitle),
    DescriptionResourceName = nameof(Resources.Description),
    AccessibleNameResourceName = nameof(Resources.AccessibleName))]
    public class JsonCompareTool : IGuiTool, IDisposable
    {
        private string? value1;

        private string? value2;

        private bool useStrictCompare = true;

        private readonly IUIMultiLineTextInput labelResult = MultiLineTextInput().ReadOnly().Text(Resources.NoResults);

        private CancellationTokenSource? lastCancellationToken;

        private readonly SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);

        public UIToolView View => new UIToolView(
            isScrollable: false,
            Grid().
                Rows(Auto, Fraction(1)).
                Cells(
                    Cell(0, 0, 1, 1, 
                        Card(
                            Grid().
                                Columns(Fraction(1), Auto).
                                Cells(
                                    Cell(0, 0, 1, 1, Label().Text(Resources.UseStrictCompare)),
                                    Cell(0, 1, 1, 1, Switch().On().OnToggle(OnStrictCompareChanged))
                                )
                        )),
                    Cell(1, 0, 1, 1, SplitGrid().
                            Horizontal().
                            TopPaneLength(new UIGridLength(2, UIGridUnitType.Fraction)).
                            BottomPaneLength(new UIGridLength(1, UIGridUnitType.Fraction)).
                            WithTopPaneChild(
                                SplitGrid().
                                    Vertical().
                                    LeftPaneLength(new UIGridLength(1, UIGridUnitType.Fraction)).
                                    RightPaneLength(new UIGridLength(1, UIGridUnitType.Fraction)).
                                    WithLeftPaneChild(MultiLineTextInput().Title(Resources.JsonOfTheFirstFile).OnTextChanged(OnValue1TextChanged).Language("json")).
                                    WithRightPaneChild(MultiLineTextInput().Title(Resources.JsonOfTheSecondFile).OnTextChanged(OnValue2TextChanged).Language("json"))).
                            WithBottomPaneChild(labelResult)))
                );

        public void OnDataReceived(string dataTypeName, object? parsedData)
        {
            
        }

        private async void OnStrictCompareChanged(bool value)
        {
            useStrictCompare = value;
            await Task.Run(DoCompare);
        }

        private async void OnValue1TextChanged(string text)
        {
            value1 = text;
            await Task.Run(DoCompare);
        }

        private async void OnValue2TextChanged(string text)
        {
            value2 = text;
            await Task.Run(DoCompare);
        }

        private async Task DoCompare()
        {
            lastCancellationToken?.Cancel();
            await semaphore.WaitAsync();
            try
            {
                lastCancellationToken = new CancellationTokenSource();
                await Task.Run(() => DoCompare(lastCancellationToken.Token));
            }
            catch (TaskCanceledException)
            {

            }
            catch (Exception e)
            {
                labelResult.Text($"{Resources.Error}\n{e.Message}");
            }
            finally
            {
                semaphore.Release();
            }
        }

        private void DoCompare(CancellationToken ct)
        {
            JToken? t1 = null;
            JToken? t2 = null;

            List<string> messages = [];

            JsonLoadSettings loadSettings = new JsonLoadSettings 
            { 
                LineInfoHandling = LineInfoHandling.Ignore, 
                CommentHandling = CommentHandling.Ignore, 
                DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error 
            };

            try
            {
                if (!string.IsNullOrEmpty(value1))
                {
                    t1 = JToken.Parse(value1, loadSettings);
                }
            }
            catch (Exception e) 
            { 
                messages.Add(Resources.ParseErrorOfTheFirstFile);
                messages.Add(e.Message);
            }

            try
            {
                if (!string.IsNullOrEmpty(value2))
                {
                    t2 = JToken.Parse(value2, loadSettings);
                }
            }
            catch (Exception e)
            {
                messages.Add(Resources.ParseErrorOfTheSecondFile);
                messages.Add(e.Message);
            }

            string joinedMessage;

            if (t1 is not null && t2 is not null)
            {
                DoCompare(t1, t2, "$root", messages, ct);
                if (messages.Count == 0)
                {
                    messages.Add(Resources.FilesAreIdentical);
                }
            } 
            
            if (messages.Count > 0)
            {
                joinedMessage = string.Join(Environment.NewLine, messages);
            }
            else
            {
                joinedMessage = string.Empty;
            }

            labelResult.Text(joinedMessage);
        }

        private void DoCompare(JToken t1, JToken t2, string path, List<string> messages, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            if (t1 is JArray a1 && t2 is JArray a2)
            {
                DoCompare(a1, a2, path, messages, ct);
            }
            else if (t1 is JObject o1 && t2 is JObject o2)
            {
                DoCompare(o1, o2, path, messages, ct);
            }
            else if (t1 is JValue v1 && t2 is JValue v2)
            {
                DoCompare(v1, v2, path, messages, ct);
            }
            else if (t1.Type != t2.Type)
            {
                messages.Add(string.Format(CultureInfo.CurrentCulture, Resources.ValuesOnPathAreDifferentTypes, path, t1.Type, t2.Type));
            }
        }

        private void DoCompare(JArray a1, JArray a2, string path, List<string> messages, CancellationToken ct)
        {
            if (a1.Count != a2.Count)
            {
                messages.Add(string.Format(CultureInfo.CurrentCulture, Resources.ArraysOnPathHaveInequalCountOfElements, path, a1.Count, a2.Count));
            }

            int min = Math.Min(a1.Count, a2.Count);

            for (int i = 0; i < min; i++)
            {
                ct.ThrowIfCancellationRequested();

                DoCompare(a1[i], a2[i], path + $"[{i}]", messages, ct);
            }
        }

        private void DoCompare(JObject o1, JObject o2, string path, List<string> messages, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            HashSet<string> keys1 = new HashSet<string>();
            foreach (JProperty p in o1.Properties())
            {
                if (!keys1.Add(p.Name))
                {
                    messages.Add(string.Format(CultureInfo.CurrentCulture, Resources.ObjectOnPathInFileHasDuplicatePropertyName, path, "first", p.Name));
                }
            }

            HashSet<string> keys2 = new HashSet<string>(keys1.Count);
            foreach (JProperty p in o2.Properties())
            {
                if (!keys2.Add(p.Name))
                {
                    messages.Add(string.Format(CultureInfo.CurrentCulture, Resources.ObjectOnPathInFileHasDuplicatePropertyName, path, "second", p.Name));
                }
            }

            HashSet<string> compare = new HashSet<string>(keys1);
            compare.ExceptWith(keys2);

            ct.ThrowIfCancellationRequested();

            if (compare.Count > 0)
            {
                foreach (string key in compare)
                {
                    ct.ThrowIfCancellationRequested();

                    messages.Add(string.Format(CultureInfo.CurrentCulture, Resources.ObjectOnPathInFileDoesNotHavePropertyWithName, path, "second", key));
                }
            }

            compare.Clear();
            compare.UnionWith(keys2);
            compare.ExceptWith(keys1);

            ct.ThrowIfCancellationRequested();

            if (compare.Count > 0)
            {
                foreach (string key in compare)
                {
                    ct.ThrowIfCancellationRequested();

                    messages.Add(string.Format(CultureInfo.CurrentCulture, Resources.ObjectOnPathInFileDoesNotHavePropertyWithName, path, "first", key));
                }
            }

            compare.Clear();
            compare.UnionWith(keys1);
            compare.IntersectWith(keys2);

            ct.ThrowIfCancellationRequested();

            foreach (string key in compare)
            {
                ct.ThrowIfCancellationRequested();

                if (o1[key] is JToken t1 && o2[key] is JToken t2)
                {
                    DoCompare(t1, t2, path + $".{key}", messages, ct);
                }
            }
        }

        private void DoCompare(JValue v1, JValue v2, string path, List<string> messages, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            static bool IsNumeric(JValue v)
            {
                return v.Type == JTokenType.Integer || v.Type == JTokenType.Float;
            }

            bool areEqual;

            if (!useStrictCompare && IsNumeric(v1) && IsNumeric(v2) && ((v1.Type == JTokenType.Float) ^ (v2.Type == JTokenType.Float)))
            {
                double d1 = v1.Value<double>();
                double d2 = v2.Value<double>();
                areEqual = d1 == d2;
            }
            else if (v1.Type == v2.Type)
            {
                areEqual = Equals(v1.Value, v2.Value);
            }
            else
            {
                messages.Add(string.Format(CultureInfo.CurrentCulture, Resources.ValuesOnPathAreDifferentTypes, path, v1.Type, v2.Type));
                return;
            }

            if (!areEqual)
            {
                messages.Add(string.Format(CultureInfo.CurrentCulture, Resources.ValuesOnPathAreNotEqual, path, GetJsonValue(v1), GetJsonValue(v2)));
            }
        }

        private static string GetJsonValue(JValue value)
        {
            using StringWriter writer = new StringWriter();
            using JsonTextWriter jsonWriter = new JsonTextWriter(writer);
            value.WriteTo(jsonWriter);
            return writer.ToString();
        }

        public void Dispose()
        {
            lastCancellationToken?.Cancel();
            lastCancellationToken?.Dispose();
            semaphore.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}

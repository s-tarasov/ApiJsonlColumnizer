using System;
using System.Collections.Generic;
using System.Linq;
using LogExpert;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ApiJsonlColumnizer
{
    public class JsonColumn
    {
        #region cTor

        public JsonColumn(string name)
        {
            Name = name;
        }

        #endregion

        #region Properties

        public string Name { get; }

        #endregion
    }

    public class ApiJsonlColumnizer : ILogLineColumnizer, IColumnizerPriority
    {

        #region Properties

        protected JsonColumn[] ColumnList = new[] {
            new JsonColumn("@timestamp"),
            new JsonColumn("api"),
            new JsonColumn("request.url"),
            new JsonColumn("response.body"),
            new JsonColumn("request.body"),           
            new JsonColumn("context.siteurl"),
            new JsonColumn("operationName"),
        };

        #endregion

        #region Public methods

        public virtual string GetName() => "Api Json Columnizer";

        public virtual string GetDescription() => "Splits JSON log files into columns.\r\n\r\nCredits:\r\nThis Columnizer uses the Newtonsoft json package.\r\n";

        public virtual int GetColumnCount() => ColumnList.Length;

        public virtual string[] GetColumnNames()
        {
            string[] names = new string[GetColumnCount()];
            int i = 0;
            foreach (var column in ColumnList)
            {
                names[i++] = column.Name;
            }

            return names;
        }

        public virtual IColumnizedLogLine SplitLine(ILogLineColumnizerCallback callback, ILogLine line)
        {
            JObject json = ParseJson(line);

            if (json != null)
            {
                return SplitJsonLine(line, json);
            }

            var cLogLine = new ColumnizedLogLine { LogLine = line };

            var columns = Column.CreateColumns(ColumnList.Length, cLogLine);

            columns.Last().FullValue = line.FullLine;

            cLogLine.ColumnValues = columns.Select(a => (IColumn)a).ToArray();

            return cLogLine;
        }

        public virtual bool IsTimeshiftImplemented() => false;

        public virtual void SetTimeOffset(int msecOffset) => throw new NotImplementedException();

        public virtual int GetTimeOffset() => throw new NotImplementedException();

        public virtual DateTime GetTimestamp(ILogLineColumnizerCallback callback, ILogLine line) => throw new NotImplementedException();

        public virtual void PushValue(ILogLineColumnizerCallback callback, int column, string value, string oldValue) => throw new NotImplementedException();

        public virtual Priority GetPriority(string fileName, IEnumerable<ILogLine> samples)
        {
            Priority result = Priority.NotSupport;
            if (fileName.EndsWith("jsonl", StringComparison.OrdinalIgnoreCase))
            {
                result = Priority.WellSupport;
            }

            return result;
        }

        #endregion

        #region Private Methods

        protected static JObject ParseJson(ILogLine line)
        {
            return JsonConvert.DeserializeObject<JObject>(line.FullLine, new JsonSerializerSettings()
            {
                Error = (sender, args) => { args.ErrorContext.Handled = true; } //We ignore the error and handle the null value
            });
        }

        public class ColumnWithName : Column
        {
            public string ColumneName { get; set; }
        }

        protected virtual IColumnizedLogLine SplitJsonLine(ILogLine line, JObject json)
        {
            var cLogLine = new ColumnizedLogLine { LogLine = line };

            var columns = GetFlatJsonChilds(json)
                .Select(property => new ColumnWithName { FullValue = property.value, ColumneName = property.path, Parent = cLogLine })
                .ToList();

            List<IColumn> returnColumns = new List<IColumn>();
            foreach (var column in ColumnList)
            {
                var existingColumn = columns.Find(x => x.ColumneName == column.Name);
                if (existingColumn != null)
                {
                    returnColumns.Add(new Column() { FullValue = existingColumn.FullValue, Parent = cLogLine });
                    continue;
                }

                // Fields that is missing in current line should be shown as empty.
                returnColumns.Add(new Column() { FullValue = "", Parent = cLogLine });
            }

            cLogLine.ColumnValues = returnColumns.ToArray();

            return cLogLine;
        }

        #endregion

        IEnumerable<(string path, string value)> GetFlatJsonChilds(JToken token)
        {
            return token
                .SelectTokens("$..*")
                .Where(t => !t.HasValues)
                .Select(t => (t.Path, t.ToString()));
        }
    }
}
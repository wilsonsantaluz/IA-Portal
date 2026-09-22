namespace Portal.AI.Gateway.Infrastructure.Trino;

public class TrinoResponseData
{
    public List<string> ColumnNames { get; private set; } = new();
    public List<Dictionary<string, object>> Rows { get; private set; } = new();

    public void AddBatch(List<TrinoRawColumn>? columns, List<List<object>>? data)
    {
        // Se chegaram colunas e ainda não conhecemos os nomes, registra
        if (columns != null && ColumnNames.Count == 0)
        {
            ColumnNames = columns.Select(c => c.Name).ToList();
        }

        // Se chegou data, processa
        if (data == null) return;

        // Se ainda não temos nomes de coluna, gera nomes genéricos (_col0, _col1, ...)
        if (ColumnNames.Count == 0 && data.Count > 0)
        {
            var colCount = data[0].Count;
            ColumnNames = Enumerable.Range(0, colCount)
                .Select(i => $"_col{i}")
                .ToList();
        }

        foreach (var row in data)
        {
            var dict = new Dictionary<string, object>();

            for (int i = 0; i < ColumnNames.Count; i++)
            {
                var colName = ColumnNames[i];
                object value = i < row.Count ? row[i] : null;
                dict[colName] = value ?? DBNull.Value;
            }

            Rows.Add(dict);
        }
    }
}

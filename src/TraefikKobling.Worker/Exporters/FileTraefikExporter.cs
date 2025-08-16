using System.Text;
using TraefikKobling.Worker.Extensions;

namespace TraefikKobling.Worker.Exporters;

public class FileTraefikExporter : ITraefikExporter
{
    private readonly string _dynamicFilePath;

    public FileTraefikExporter(ILogger<FileTraefikExporter> logger,string dynamicFilePath)
    {
        _dynamicFilePath = dynamicFilePath;
        logger.LogInformation("Exporting Traefik configuration to file: {file}", _dynamicFilePath);
    }

    public async Task ExportTraefikEntries(IDictionary<string, string> oldEntries, IDictionary<string, string> newEntries, CancellationToken cancellationToken)
    {
        if (oldEntries.SetEquals(newEntries))
            return;
        
        var root = new Node("traefik", [],[]);

        foreach (var (key, value) in newEntries)
        {
            var entryNode = Node.Create(key.Split("/"), value);
            root.Merge(entryNode);
        }
        
        var bld = new StringBuilder();
        GenerateFile(bld, root.Children);
        var content = bld.ToString();

        await using var file = File.Open(_dynamicFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
        await using var writer = new StreamWriter(file);
        await writer.WriteAsync(content);
    }

    private void GenerateFile(StringBuilder bld, IList<Node> nodes, int indentation = 0, int step = 4, char indentChar = ' ')
    {
        foreach (var node in nodes)
        {
            if (int.TryParse(node.Key, out _))
            {
                if (node.Children.Count > 0)
                {
                    bld.Indent(indentation,indentChar)
                        .Append("- ")
                        .Append(node.Children[0].Key)
                        .Append(": ")
                        .Append(node.Children[0].Leaves[0].Value)
                        .AppendLine();

                    foreach (var child in node.Children.Skip(1))
                    {
                        bld.Indent(indentation,indentChar)
                            .Append("  ")
                            .Append(child.Key)
                            .Append(": ")
                            .Append(child.Leaves[0].Value)
                            .AppendLine();
                    }
                }
                else if (node.Leaves.Count > 0)
                {
                    foreach (var leaf in node.Leaves)
                    {
                        bld.Indent(indentation,indentChar)
                            .Append("- ")
                            .Append(leaf.Value)
                            .AppendLine();
                    }
                }

                continue;
            }
            
            
            bld.Indent(indentation,indentChar)
                .Append(node.Key)
                .Append(':');


            if (node.Children.IsEmpty())
            {
                if (node.Leaves.Count == 1)
                    bld.Append(' ')
                        .Append(node.Leaves[0].Value)
                        .AppendLine();
            }
            else
            {
                GenerateFile(bld.AppendLine(), node.Children, indentation + step);
            }
        }
    }

    private record Node(string Key, List<Node> Children, List<Leaf> Leaves)
    {
        internal static Node Create(Span<string> path, string value)
        {
            return path switch
            {
                [var last] => new Node(last, [], [new Leaf(value)]),
                [var first, .. var other]=> new Node(first, [Create(other,value)],[]),
                _ => throw new ArgumentOutOfRangeException(nameof(path))
            };
        }
        
        internal void Merge(Node other)
        {
            if (Key != other.Key) 
                throw new InvalidOperationException("Node key mismatch");
            
            foreach (var otherChild in other.Children)
            {
                var repeatedChildren = Children.FirstOrDefault(t => t.Key == otherChild.Key);
                if (repeatedChildren is not null)
                {
                    repeatedChildren.Merge(otherChild);
                    continue;
                }
                Children.Add(otherChild);
            }
                
            Leaves.AddRange(other.Leaves);

        }
    }
    private record Leaf(string Value);
}
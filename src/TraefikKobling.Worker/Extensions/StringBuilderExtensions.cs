using System.Text;

namespace TraefikKobling.Worker.Extensions;

public static class StringBuilderExtensions
{
    public static StringBuilder Indent(this StringBuilder builder, int level, char character = '\t')
    {
        for (int i = 0; i < level; i++)
        {
            builder.Append(character);
        }
        return builder;
    }
}
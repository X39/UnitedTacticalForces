using System.Text;
using System.Xml.Serialization;
using HtmlAgilityPack;

namespace X39.UnitedTacticalForces.Api.Helpers;

public static class Arma3ModPackParser
{
    public record struct TitleLinkPair(string Title, string Link);

    public static (string title, IReadOnlyCollection<TitleLinkPair> Mods, IReadOnlyCollection<TitleLinkPair>
        Dependencies) FromHtml(
            string html)
    {
        // ToDo: Fix Arma Launcher/Mods/"Mehr"/"Modliste in Datei exportieren" not being parsed due to title etc. missing (also make this no longer throw an exception if it's not a valid preset)
        // Get all html tables using HtmlAgilityPack
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Read in the contents of a <strong> tag living in the first <h1> tag, into a string called Title.
        var title = doc.DocumentNode.SelectSingleNode("//h1/strong").InnerText;

        var tables = doc.DocumentNode.SelectNodes("//table");
        // Read in the contents of the first and the link contained in the third column into a list of TitleLinkPair structs.
        // <tr data-type="ModContainer">
        //   <td data-type="DisplayName">CBA_A3</td>
        //   <td>
        //     <span class="from-steam">Steam</span>
        //   </td>
        //   <td>
        //     <a href="https://steamcommunity.com/sharedfiles/filedetails/?id=450814997" data-type="Link">https://steamcommunity.com/sharedfiles/filedetails/?id=450814997</a>
        //   </td>
        // </tr>
        var mods = tables[0]
            .SelectNodes("//tr[@data-type='ModContainer']")
            ?.Select(
                tr => new TitleLinkPair(
                    tr.SelectSingleNode("td[@data-type='DisplayName']").InnerText,
                    tr.SelectSingleNode("td/a[@data-type='Link']").Attributes["href"].Value))
            .ToList() ?? [];

        // Read in the contents of the first and second column into a list of TitleLinkPair structs.
        // <tr data-type="DlcContainer">
        //   <td data-type="DisplayName">S.O.G. Prairie Fire</td>
        //   <td>
        //     <a href="https://store.steampowered.com/app/1227700" data-type="Link">https://store.steampowered.com/app/1227700</a>
        //   </td>
        // </tr>
        var dependencies = tables[1]
            .SelectNodes("//tr[@data-type='DlcContainer']")
            ?.Select(
                tr => new TitleLinkPair(
                    tr.SelectSingleNode("td[@data-type='DisplayName']").InnerText,
                    tr.SelectSingleNode("td/a[@data-type='Link']").Attributes["href"].Value))
            .ToList() ?? [];

        return (title, mods.AsReadOnly(), dependencies.AsReadOnly());
    }

    public static string ToHtml(
        string title,
        IEnumerable<string> mods,
        IEnumerable<string> dependencies)
    {
        return ToHtml(
            title,
            mods.Select(q => new TitleLinkPair(q, q)),
            dependencies.Select(q => new TitleLinkPair(q, q)));
    }

    public static string ToHtml(
        string title,
        IEnumerable<TitleLinkPair> mods,
        IEnumerable<TitleLinkPair> dependencies)
    {
        if (dependencies == null) throw new ArgumentNullException(nameof(dependencies));
        var builder = new StringBuilder();
        builder.AppendLine(HtmlStart);
        builder.AppendLine("    <!-- Created by X39.UnitedTacticalForces.Api.Helpers.Arma3ModPackParser -->");
        builder.AppendLine(HeaderStart);
        builder.AppendLine($"""
        <meta name="arma:PresetName" content="{title}" />
        <meta name="generator" content="X39.UnitedTacticalForces.Api.Helpers.Arma3ModPackParser - https://github.com/X39/UnitedTacticalForces" />
""");
        builder.AppendLine(Style);
        builder.AppendLine(HeaderEnd);
        builder.AppendLine("    <body>");
        builder.AppendLine($"        <h1>Arma 3  - Preset <strong>{title}</strong></h1>");
        builder.AppendLine(ImportHint);
        builder.AppendLine("        <div class=\"mod-list\">");
        builder.AppendLine("            <table>");
        foreach (var mod in mods)
        {
            builder.AppendLine("                <tr data-type=\"ModContainer\">");
            builder.AppendLine($"                    <td data-type=\"DisplayName\">{mod.Title}</td>");
            builder.AppendLine($"                    <td><span class=\"from-steam\">Steam</span></td>");
            builder.AppendLine(
                $"                    <td><a href=\"{mod.Link}\" data-type=\"Link\">{mod.Link}</a></td>");
            builder.AppendLine("                </tr>");
        }

        builder.AppendLine("            </table>");
        builder.AppendLine("        </div>");
        builder.AppendLine("        <div class=\"dlc-list\">");
        builder.AppendLine("            <table>");
        foreach (var mod in dependencies)
        {
            builder.AppendLine("                <tr data-type=\"DlcContainer\">");
            builder.AppendLine($"                    <td data-type=\"DisplayName\">{mod.Title}</td>");
            builder.AppendLine(
                $"                    <td><a href=\"{mod.Link}\" data-type=\"Link\">{mod.Link}</a></td>");
            builder.AppendLine("                </tr>");
        }

        builder.AppendLine("            </table>");
        builder.AppendLine("        </div>");
        builder.AppendLine("    </body>");
        builder.AppendLine(HtmlEnd);

        return builder.ToString();
    }

    private const string HtmlStart =
        """
<?xml version="1.0" encoding="utf-8"?>
<html>
""";

    private const string HtmlEnd =
        """
</html>
""";

    private const string ImportHint =
        """
    <p class="before-list">
      <em>To import this preset, drag this file onto the Launcher window. Or click the MODS tab, then PRESET in the top right, then IMPORT at the bottom, and finally select this file.</em>
    </p>
""";

    private const string HeaderEnd =
        """
    </head>
""";

    private const string HeaderStart =
        """
    <head>
        <meta name="arma:Type" content="preset" />
""";

    private const string Style =
        """
        <title>Arma 3</title>
        <link href="https://fonts.googleapis.com/css?family=Roboto" rel="stylesheet" type="text/css" />
        <style>
            body {
	            margin: 0;
	            padding: 0;
	            color: #fff;
	            background: #000;	
            }
            body, th, td { font: 95%/1.3 Roboto, Segoe UI, Tahoma, Arial, Helvetica, sans-serif; }
            td { padding: 3px 30px 3px 0; }
            h1 {
                padding: 20px 20px 0 20px;
                color: white;
                font-weight: 200;
                font-family: segoe ui;
                font-size: 3em;
                margin: 0;
            }
            em { font-variant: italic; color:silver; }
            .before-list { padding: 5px 20px 10px 20px; }
            .mod-list { background: #222222; padding: 20px; }
            .dlc-list { background: #222222; padding: 20px; }
            .footer { padding: 20px; color:gray; }
            .whups { color:gray; }
            a { color: #D18F21; text-decoration: underline; }
            a:hover { color:#F1AF41; text-decoration: none; }
            .from-steam { color: #449EBD; }
            .from-local { color: gray; }
        </style>
""";
}
using System.IO.Compression;
using System.Text;
using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Infrastructure.Compliance;

namespace ComplianceTracker.UnitTests.Compliance;

// Edge-case unit tests of US-149: every diagram reader, the limits, and what the AI reads. Arrange / Act / Assert.
public class DiagramReaderTests
{
    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);

    private static readonly DiagramReaders Readers = new(
        [new DrawioDiagramReader(), new MermaidDiagramReader(), new PlantUmlDiagramReader(), new BpmnDiagramReader(), new SvgDiagramReader()]);

    private const string Drawio = """
        <mxfile><diagram id="p1" name="Page-1"><mxGraphModel><root>
          <mxCell id="0"/><mxCell id="1" parent="0"/>
          <mxCell id="w" value="Web app" style="rounded=1;" vertex="1" parent="1"/>
          <mxCell id="a" value="&lt;b&gt;API&lt;/b&gt;" vertex="1" parent="1"/>
          <mxCell id="d" value="Database" style="shape=cylinder3;" vertex="1" parent="1"/>
          <mxCell id="e1" value="HTTPS" edge="1" source="w" target="a" parent="1"/>
          <mxCell id="e2" edge="1" source="a" target="d" parent="1"/>
          <mxCell id="l2" value="SQL" style="edgeLabel;" vertex="1" parent="e2"/>
        </root></mxGraphModel></diagram></mxfile>
        """;

    // ---------------------------------------------------------- draw.io

    [Fact]
    public void A_drawio_diagram_gives_its_shapes_and_arrows_with_labels_also_on_the_arrow()
    {
        var diagram = new DrawioDiagramReader().Read(Utf8(Drawio));

        Assert.Equal(["Web app", "API", "Database"], diagram.Elements.Select(e => e.Name));
        Assert.Equal("database", diagram.Elements[2].Kind);
        Assert.Equal(["Web app → API: HTTPS", "API → Database: SQL"], diagram.Links.Select(l => l.ToString()));
        Assert.Equal("Diagram: 3 elements, 2 links", diagram.Summary);
    }

    [Fact]
    public void A_compressed_drawio_page_is_unpacked()
    {
        var model = Drawio[(Drawio.IndexOf("<mxGraphModel>", StringComparison.Ordinal))..(Drawio.IndexOf("</diagram>", StringComparison.Ordinal))];
        using var packed = new MemoryStream();
        using (var deflate = new DeflateStream(packed, CompressionLevel.Optimal, leaveOpen: true))
        {
            deflate.Write(Utf8(Uri.EscapeDataString(model)));
        }

        var file = $"<mxfile><diagram id=\"p1\">{Convert.ToBase64String(packed.ToArray())}</diagram></mxfile>";

        Assert.Equal(3, new DrawioDiagramReader().Read(Utf8(file)).Elements.Count);
    }

    [Fact]
    public void A_drawio_page_that_cannot_be_unpacked_says_so()
        => Assert.Equal("the compressed draw.io page could not be unpacked",
            Assert.Throws<DiagramUnreadableException>(() => new DrawioDiagramReader().Read(Utf8("<mxfile><diagram>not base64!</diagram></mxfile>"))).Message);

    [Fact]
    public void An_empty_drawio_file_has_no_diagram()
        => Assert.Equal("no diagram was found in the file",
            Assert.Throws<DiagramUnreadableException>(() => new DrawioDiagramReader().Read(Utf8("<mxfile></mxfile>"))).Message);

    [Fact]
    public void An_arrow_to_nowhere_is_left_out()
    {
        var file = Drawio.Replace("target=\"d\"", "target=\"gone\"");

        Assert.Single(new DrawioDiagramReader().Read(Utf8(file)).Links);
    }

    // ---------------------------------------------------------- R2: nothing is run, nothing is fetched

    [Fact]
    public void A_file_with_a_DTD_is_refused_so_no_entity_is_ever_expanded()
    {
        var xxe = """<?xml version="1.0"?><!DOCTYPE m [<!ENTITY x SYSTEM "file:///etc/passwd">]><mxfile><diagram>&x;</diagram></mxfile>""";

        Assert.Equal("the file has a DTD, which is not allowed", Assert.Throws<DiagramUnreadableException>(() => new DrawioDiagramReader().Read(Utf8(xxe))).Message);
    }

    [Fact]
    public void An_svg_gives_its_text_but_never_its_script_or_style()
    {
        var svg = """<svg xmlns="http://www.w3.org/2000/svg"><script>alert('x')</script><style>.a{}</style><text>Login</text><text><tspan>Check</tspan> <tspan>MFA</tspan></text></svg>""";

        var diagram = new SvgDiagramReader().Read(Utf8(svg));

        Assert.Equal(["Login", "Check MFA"], diagram.Elements.Select(e => e.Name));
        Assert.DoesNotContain(diagram.Elements, e => e.Name.Contains("alert", StringComparison.Ordinal));
    }

    [Fact]
    public void A_drawio_export_as_svg_is_read_as_the_diagram_it_carries()
    {
        var svg = $"<svg xmlns=\"http://www.w3.org/2000/svg\" content=\"{System.Security.SecurityElement.Escape(Drawio)}\"><text>ignored</text></svg>";

        var diagram = new SvgDiagramReader().Read(Utf8(svg));

        Assert.Equal((DiagramFormat.Svg, 2), (diagram.Format, diagram.Links.Count));
    }

    [Fact]
    public void A_file_that_is_not_an_svg_says_so()
        => Assert.Throws<DiagramUnreadableException>(() => new SvgDiagramReader().Read(Utf8("<html><body>x</body></html>")));

    // ---------------------------------------------------------- Mermaid

    [Fact]
    public void A_mermaid_flowchart_gives_its_nodes_by_label_and_arrows_with_labels()
    {
        var diagram = new MermaidDiagramReader().Read(Utf8("flowchart LR\n  A[Web app] -->|HTTPS| B(API)\n  B -- SQL --> C[(Database)]\n  %% a comment\n  style A fill:#f9f"));

        Assert.Equal(["Web app", "API", "Database"], diagram.Elements.Select(e => e.Name));
        Assert.Equal(["Web app → API: HTTPS", "API → Database: SQL"], diagram.Links.Select(l => l.ToString()));
    }

    [Fact]
    public void A_mermaid_chain_gives_one_arrow_per_step()
        => Assert.Equal(2, new MermaidDiagramReader().Read(Utf8("graph TD\nA --> B --> C")).Links.Count);

    [Fact]
    public void A_mermaid_sequence_diagram_gives_participants_and_messages()
    {
        var diagram = new MermaidDiagramReader().Read(Utf8("sequenceDiagram\nparticipant U as Clinician\nU->>API: Sign in with MFA\nAPI-->>U: Token"));

        Assert.Equal("participant", diagram.Elements[0].Kind);
        Assert.Equal(["Clinician → API: Sign in with MFA", "API → Clinician: Token"], diagram.Links.Select(l => l.ToString()));
    }

    [Fact]
    public void A_mermaid_state_diagram_reads_the_event_after_the_colon()
        => Assert.Equal("Locked → Open: correct PIN",
            new MermaidDiagramReader().Read(Utf8("stateDiagram-v2\nLocked --> Open : correct PIN")).Links.Single().ToString());

    [Fact]
    public void An_empty_mermaid_file_says_so()
        => Assert.Equal("the diagram is empty", Assert.Throws<DiagramUnreadableException>(() => new MermaidDiagramReader().Read(Utf8("\n  \n"))).Message);

    // ---------------------------------------------------------- PlantUML

    [Fact]
    public void A_plantuml_use_case_diagram_gives_actors_use_cases_and_links_by_name()
    {
        var diagram = new PlantUmlDiagramReader().Read(Utf8("@startuml\n' comment\nactor Clinician\nusecase (Sign in with MFA) as UC1\nClinician --> UC1\n@enduml"));

        Assert.Equal([("Clinician", "actor"), ("Sign in with MFA", "usecase")], diagram.Elements.Select(e => (e.Name, e.Kind)));
        Assert.Equal("Clinician → Sign in with MFA", diagram.Links.Single().ToString());
    }

    [Fact]
    public void A_plantuml_component_diagram_reads_quoted_names_aliases_shorthands_and_labels()
    {
        var diagram = new PlantUmlDiagramReader().Read(Utf8("@startuml\ncomponent \"Web app\" as W <<frontend>>\ndatabase DB\n[API] as A\nW -down-> A : HTTPS\nA ..> DB : SQL\n@enduml"));

        Assert.Equal(["Web app", "DB", "API"], diagram.Elements.Select(e => e.Name));
        Assert.Equal(["Web app → API: HTTPS", "API → DB: SQL"], diagram.Links.Select(l => l.ToString()));
    }

    [Fact]
    public void A_plantuml_arrow_to_an_undeclared_name_declares_it()
        => Assert.Equal(2, new PlantUmlDiagramReader().Read(Utf8("@startuml\nAlice -> Bob : hi\n@enduml")).Elements.Count);

    // ---------------------------------------------------------- BPMN

    [Fact]
    public void A_bpmn_process_gives_its_tasks_events_gateways_and_flows()
    {
        var bpmn = """
            <bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL"><bpmn:process id="p">
              <bpmn:startEvent id="s"/><bpmn:userTask id="t" name="Approve release"/>
              <bpmn:exclusiveGateway id="g" name="Approved?"/><bpmn:endEvent id="e" name="Released"/>
              <bpmn:sequenceFlow id="f1" sourceRef="s" targetRef="t"/><bpmn:sequenceFlow id="f2" sourceRef="t" targetRef="g"/>
              <bpmn:sequenceFlow id="f3" name="yes" sourceRef="g" targetRef="e"/><bpmn:sequenceFlow id="f4" sourceRef="g" targetRef="missing"/>
            </bpmn:process></bpmn:definitions>
            """;

        var diagram = new BpmnDiagramReader().Read(Utf8(bpmn));

        Assert.Equal([("Start event", "event"), ("Approve release", "task"), ("Approved?", "gateway"), ("Released", "event")], diagram.Elements.Select(e => (e.Name, e.Kind)));
        Assert.Equal(["Start event → Approve release", "Approve release → Approved?", "Approved? → Released: yes"], diagram.Links.Select(l => l.ToString()));
    }

    [Fact]
    public void A_broken_bpmn_file_is_not_well_formed()
        => Assert.Equal("the XML is not well-formed", Assert.Throws<DiagramUnreadableException>(() => new BpmnDiagramReader().Read(Utf8("<definitions><process>"))).Message);

    [Fact]
    public void An_xml_file_that_is_not_bpmn_says_so()
        => Assert.Throws<DiagramUnreadableException>(() => new BpmnDiagramReader().Read(Utf8("<note>hi</note>")));

    // ---------------------------------------------------------- Limits and cleaning (R5)

    [Fact]
    public void At_most_2000_elements_are_kept_and_the_cut_is_said()
    {
        var diagram = DiagramContent.Of(DiagramFormat.Mermaid, Enumerable.Range(1, 2500).Select(i => new DiagramElement($"Box {i}", "node")), []);

        Assert.Equal((2000, 2500), (diagram.Elements.Count, diagram.TotalElements));
        Assert.Equal("Read the first 2,000 of 2,500 elements", diagram.CutNote);
    }

    [Theory]
    [InlineData("<div>Web<br>app</div>", "Web app")]
    [InlineData("  Line\nbreak  ", "Line break")]
    [InlineData("a &amp; b", "a & b")]
    [InlineData("   ", "")]
    public void Names_are_cleaned_to_one_line_of_plain_text(string raw, string expected)
        => Assert.Equal(expected, DiagramContent.Clean(raw));

    [Fact]
    public void Empty_and_repeated_elements_are_left_out()
    {
        var diagram = DiagramContent.Of(DiagramFormat.Svg, [new("A", "text"), new("A", "text"), new(" ", "text")], []);

        Assert.Single(diagram.Elements);
    }

    // ---------------------------------------------------------- Picking the reader, and what the AI reads (R3, R4)

    [Fact]
    public void A_file_that_is_not_a_diagram_is_not_read_as_one()
        => Assert.Null(Readers.Read("application/pdf", Utf8("%PDF-")));

    [Fact]
    public void A_reader_error_becomes_the_reason_and_an_unexpected_one_a_general_reason()
    {
        Assert.Equal("the XML is not well-formed", Readers.Read("application/bpmn+xml", Utf8("<x"))!.Value.Error);
        Assert.Null(Readers.Read("application/bpmn+xml", Utf8("<x"))!.Value.Content);
    }

    [Fact]
    public void The_AI_reads_what_the_diagram_says()
    {
        var text = new EvidenceTextReader(Readers).Read("application/vnd.jgraph.mxfile", Utf8(Drawio));

        Assert.Equal("Elements: Web app; API; Database\nWeb app → API: HTTPS\nAPI → Database: SQL", text!.Replace("\r\n", "\n"));
    }

    [Fact]
    public void A_diagram_the_AI_cannot_read_gives_no_text()
        => Assert.Null(new EvidenceTextReader(Readers).Read("application/bpmn+xml", Utf8("<broken")));

    // ---------------------------------------------------------- The evidence (Domain)

    [Theory]
    [InlineData("flow.mmd", "text/vnd.mermaid")]
    [InlineData("use-cases.PUML", "text/vnd.plantuml")]
    [InlineData("architecture.drawio", "application/vnd.jgraph.mxfile")]
    [InlineData("process.bpmn", "application/bpmn+xml")]
    [InlineData("flow.svg", "image/svg+xml")]
    public void Diagram_files_are_accepted_as_evidence(string name, string contentType)
        => Assert.Equal(contentType, EvidenceFile.Create(name, Utf8("graph TD")).ContentType);

    [Fact]
    public void A_binary_file_named_as_a_diagram_is_refused()
        => Assert.Throws<DomainException>(() => EvidenceFile.Create("evil.svg", [0x89, 0x50, 0x00, 0x47]));

    [Fact]
    public void An_evidence_keeps_the_diagram_or_the_reason_it_could_not_be_read()
    {
        var file = EvidenceFile.Create("a.mmd", Utf8("graph TD\nA-->B"));
        var evidence = Evidence.ForFile(Guid.NewGuid(), "Flow", file, null, Guid.NewGuid(), null, DateTime.UtcNow);

        evidence.ReadAsDiagram(new MermaidDiagramReader().Read(file.Content));
        Assert.Equal(("Diagram: 2 elements, 1 link", "A → B"), (evidence.DiagramSummary, evidence.DiagramLinks));

        evidence.DiagramUnreadable("the diagram is empty");
        Assert.Equal(("The diagram could not be read", (int?)null), (evidence.DiagramSummary, evidence.DiagramElementCount));
    }
}

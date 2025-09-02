import WidgetKit
import SwiftUI

struct Provider: TimelineProvider {
    func placeholder(in context: Context) -> SimpleEntry { SimpleEntry(date: Date(), title: "LiftLog") }
    func getSnapshot(in context: Context, completion: @escaping (SimpleEntry) -> ()) { completion(SimpleEntry(date: Date(), title: "LiftLog")) }
    func getTimeline(in context: Context, completion: @escaping (Timeline<SimpleEntry>) -> ()) {
        let entry = SimpleEntry(date: Date(), title: "Next set")
        completion(Timeline(entries: [entry], policy: .after(Date().addingTimeInterval(900))))
    }
}

struct SimpleEntry: TimelineEntry { let date: Date; let title: String }

struct LiftLogWidgetEntryView : View {
    var entry: Provider.Entry
    var body: some View {
        VStack(alignment: .leading) {
            Text(entry.title).font(.headline)
            Text(Date(), style: .time).foregroundStyle(.secondary)
        }.padding()
    }
}

struct LiftLogWidget: Widget {
    let kind: String = "LiftLogWidget"
    var body: some WidgetConfiguration {
        StaticConfiguration(kind: kind, provider: Provider()) { entry in
            LiftLogWidgetEntryView(entry: entry)
        }
        .configurationDisplayName("LiftLog")
        .description("Shows quick training info.")
        .supportedFamilies([.systemSmall, .systemMedium])
    }
}

@main
struct LiftLogWidgetBundle: WidgetBundle {
    var body: some Widget { LiftLogWidget() }
}


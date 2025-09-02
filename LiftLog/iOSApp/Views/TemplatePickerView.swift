import SwiftUI
import SwiftData

struct TemplatePickerView: View {
    @Environment(\.modelContext) private var context
    @EnvironmentObject private var session: WorkoutSessionViewModel
    @Query(sort: [\.name]) private var templates: [Template]
    let unit: UnitSystem
    @Environment(\.dismiss) private var dismiss

    var body: some View {
        List(templates) { template in
            Button {
                session.startWithTemplate(context: context, unit: unit, template: template)
                dismiss()
            } label: {
                HStack {
                    VStack(alignment: .leading) {
                        Text(template.name).font(.headline)
                        Text("\(template.items.count) items").foregroundStyle(.secondary)
                    }
                    Spacer()
                    Image(systemName: "chevron.right")
                        .foregroundStyle(.tertiary)
                }
            }
        }
        .navigationTitle("Templates")
    }
}

#Preview { TemplatePickerView(unit: .kg).environmentObject(WorkoutSessionViewModel()) }


import SwiftUI
import SwiftData

struct HistoryView: View {
    @Environment(\.modelContext) private var context
    @State private var query: String = ""
    @State private var results: [Workout] = []
    @Query(sort: [\.name]) private var tags: [Tag]
    @State private var vm = HistoryViewModel()

    var body: some View {
        NavigationStack {
            VStack {
                HStack {
                    TextField("Search by note/exercise/tag", text: $query)
                        .textFieldStyle(.roundedBorder)
                    Button {
                        Task { try? results = vm.workouts(matching: query, in: context) }
                    } label: { Image(systemName: "magnifyingglass") }
                }
                ScrollView(.horizontal, showsIndicators: false) {
                    HStack {
                        ForEach(tags) { tag in
                            Button {
                                query = tag.name
                                Task { try? results = vm.workouts(matching: query, in: context) }
                            } label: {
                                Text("#\(tag.name)")
                                    .padding(.horizontal, 10).padding(.vertical, 6)
                                    .background(Capsule().fill(Color(.secondarySystemBackground)))
                            }
                        }
                    }
                    .padding(.vertical, 4)
                }
                List(results) { w in
                    VStack(alignment: .leading) {
                        Text(w.date, style: .date).font(.headline)
                        Text("Sets: \(w.sets.count)")
                    }
                }
                .onAppear { Task { try? results = vm.workouts(matching: "", in: context) } }
            }
            .padding()
            .navigationTitle("History")
        }
    }
}

#Preview { HistoryView() }

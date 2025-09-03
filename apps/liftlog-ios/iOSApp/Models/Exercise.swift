import Foundation
import SwiftData

@Model
final class Tag {
    @Attribute(.unique) var name: String
    init(name: String) { self.name = name }
}

@Model
final class Exercise {
    @Attribute(.unique) var id: UUID
    var name: String
    var notes: String?
    var isBodyweight: Bool
    @Relationship(inverse: \TaggedExercise.exercises) var tags: [Tag]

    init(id: UUID = UUID(), name: String, notes: String? = nil, isBodyweight: Bool = false, tags: [Tag] = []) {
        self.id = id
        self.name = name
        self.notes = notes
        self.isBodyweight = isBodyweight
        self.tags = tags
    }
}

// Helper model to support many-to-many in SwiftData (optional if using arrays directly)
@Model
final class TaggedExercise {
    var tag: Tag
    @Relationship var exercises: [Exercise]
    init(tag: Tag, exercises: [Exercise] = []) {
        self.tag = tag
        self.exercises = exercises
    }
}


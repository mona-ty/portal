import Foundation
import Speech
import AVFoundation

final class SpeechRecognizer: NSObject, ObservableObject {
    @Published var isAuthorized: Bool = false
    @Published var isListening: Bool = false
    @Published var transcript: String = ""

    private let audioEngine = AVAudioEngine()
    private var speechRecognizer: SFSpeechRecognizer? = SFSpeechRecognizer(locale: Locale(identifier: "ja-JP"))
    private var request: SFSpeechAudioBufferRecognitionRequest?
    private var task: SFSpeechRecognitionTask?

    override init() {
        super.init()
        requestAuthorization()
    }

    func requestAuthorization() {
        SFSpeechRecognizer.requestAuthorization { status in
            DispatchQueue.main.async {
                self.isAuthorized = (status == .authorized)
            }
        }
    }

    func start() throws {
        guard !audioEngine.isRunning else { return }
        guard isAuthorized else { throw NSError(domain: "Speech", code: 1, userInfo: [NSLocalizedDescriptionKey: "Speech not authorized"]) }

        let session = AVAudioSession.sharedInstance()
        try session.setCategory(.record, mode: .measurement, options: .duckOthers)
        try session.setActive(true, options: .notifyOthersOnDeactivation)

        request = SFSpeechAudioBufferRecognitionRequest()
        request?.shouldReportPartialResults = true

        guard let inputNode = audioEngine.inputNode as AVAudioInputNode? else { return }
        let recordingFormat = inputNode.outputFormat(forBus: 0)
        inputNode.removeTap(onBus: 0)
        inputNode.installTap(onBus: 0, bufferSize: 1024, format: recordingFormat) { [weak self] buffer, _ in
            self?.request?.append(buffer)
        }

        audioEngine.prepare()
        try audioEngine.start()
        self.isListening = true
        self.transcript = ""

        task = speechRecognizer?.recognitionTask(with: request!, resultHandler: { [weak self] result, error in
            guard let self = self else { return }
            if let result = result {
                let text = result.bestTranscription.formattedString
                DispatchQueue.main.async { self.transcript = text }
            }
            if error != nil || (result?.isFinal ?? false) {
                self.stop()
            }
        })
    }

    func stop() {
        audioEngine.stop()
        audioEngine.inputNode.removeTap(onBus: 0)
        request?.endAudio()
        task?.cancel()
        isListening = false
    }
}

name: Bug report
description: Report a bug to help us improve
labels: [bug]
body:
  - type: textarea
    id: summary
    attributes:
      label: Summary
      description: What happened? What did you expect?
    validations:
      required: true
  - type: textarea
    id: steps
    attributes:
      label: Steps to reproduce
      description: Clear steps to reproduce the issue
      placeholder: |
        1. Go to ...
        2. Run ...
        3. See error ...
  - type: input
    id: env
    attributes:
      label: Environment
      placeholder: OS, Python/Xcode versions, etc.
  - type: textarea
    id: logs
    attributes:
      label: Logs/Screenshots
      render: shell


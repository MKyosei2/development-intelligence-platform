# Webhook API

対応Eventはpull_request、push、workflow_run、issues、check_runです。`X-Hub-Signature-256`を検証し、`X-GitHub-Delivery`を冪等Keyにします。

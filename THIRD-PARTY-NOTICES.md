# Third-party notices and upstream references

This integration package invokes the following upstream GitHub Actions; it does not vendor their implementation source into this ZIP:

- Android SDK setup: https://github.com/android-actions/setup-android — MIT License (upstream repository).
- Gradle Actions: https://github.com/gradle/actions — see upstream LICENSE and DISTRIBUTION.md for the selected caching provider; this workflow explicitly selects `cache-provider: basic`.
- Android signing: https://github.com/r0adkll/sign-android-release — MIT License (upstream repository).
- GitHub release publishing: https://github.com/softprops/action-gh-release — MIT License (upstream repository).
- GitHub maintained actions: https://github.com/actions/checkout, https://github.com/actions/setup-python, https://github.com/actions/setup-java, https://github.com/actions/upload-artifact, https://github.com/actions/download-artifact — see each upstream LICENSE.

AndroidForge and android-apk-template were used as functional references for source-ZIP handling and the CI/release workflow shape. Their original project files and sample-app binaries are not redistributed in this integration ZIP.

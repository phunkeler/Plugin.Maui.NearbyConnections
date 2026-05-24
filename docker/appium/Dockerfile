FROM node:lts-bookworm-slim

RUN apt-get update && apt-get install -y \
        default-jdk \
        android-tools-adb \
    && rm -rf /var/lib/apt/lists/*

# UiAutomator2 driver requires ANDROID_HOME with platform-tools/adb present.
# adb is installed via apt to /usr/bin/adb — symlink it into the expected SDK layout.
ENV ANDROID_HOME=/opt/android-sdk
ENV PATH=$PATH:$ANDROID_HOME/platform-tools
RUN mkdir -p $ANDROID_HOME/platform-tools \
    && ln -s /usr/bin/adb $ANDROID_HOME/platform-tools/adb

RUN npm install -g appium && appium driver install uiautomator2

EXPOSE 4723
CMD ["appium", "--address", "0.0.0.0", "--port", "4723"]

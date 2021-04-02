# Copyright (C) 2009 The Android Open Source Project
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#      http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.
#
LOCAL_PATH := $(call my-dir)

include $(CLEAR_VARS)
LOCAL_MODULE := curl
LOCAL_SRC_FILES := $(LOCAL_PATH)/android/curl-$(TARGET_ARCH_ABI)/lib/libcurl.a
include $(PREBUILT_STATIC_LIBRARY)

include $(CLEAR_VARS)
LOCAL_MODULE := nghttp2
LOCAL_SRC_FILES := $(LOCAL_PATH)/android/nghttp2-$(TARGET_ARCH_ABI)/lib/libnghttp2.a
include $(PREBUILT_STATIC_LIBRARY)

include $(CLEAR_VARS)
LOCAL_MODULE := openssl_crypto
LOCAL_SRC_FILES := $(LOCAL_PATH)/android/openssl-$(TARGET_ARCH_ABI)/lib/libcrypto.a
include $(PREBUILT_STATIC_LIBRARY)

include $(CLEAR_VARS)
LOCAL_MODULE := openssl_ssl
LOCAL_SRC_FILES := $(LOCAL_PATH)/android/openssl-$(TARGET_ARCH_ABI)/lib/libssl.a
include $(PREBUILT_STATIC_LIBRARY)

include $(CLEAR_VARS)

LOCAL_MODULE    := h2downloader
LOCAL_C_INCLUDES := $(LOCAL_PATH)/../../curl_test/include/

LOCAL_SRC_FILES := ../../curl_test/main.cpp
LOCAL_CFLAGS := -std=c++11
LOCAL_LDLIBS := -lz

LOCAL_STATIC_LIBRARIES := \
                 curl
LOCAL_WHOLE_STATIC_LIBRARIES := \
                 nghttp2 \
                 openssl_crypto \
                 openssl_ssl

include $(BUILD_SHARED_LIBRARY)

/***************************************************************************
 *                                  _   _ ____  _
 *  Project                     ___| | | |  _ \| |
 *                             / __| | | | |_) | |
 *                            | (__| |_| |  _ <| |___
 *                             \___|\___/|_| \_\_____|
 *
 * Copyright (C) 1998 - 2019, Daniel Stenberg, <daniel@haxx.se>, et al.
 *
 * This software is licensed as described in the file COPYING, which
 * you should have received as part of this distribution. The terms
 * are also available at https://curl.haxx.se/docs/copyright.html.
 *
 * You may opt to use, copy, modify, merge, publish, distribute and/or sell
 * copies of the Software, and permit persons to whom the Software is
 * furnished to do so, under the terms of the COPYING file.
 *
 * This software is distributed on an "AS IS" basis, WITHOUT WARRANTY OF ANY
 * KIND, either express or implied.
 *
 ***************************************************************************/ 

// #define DEBUG (1)


#ifdef _WIN32
#define _CRT_SECURE_NO_WARNINGS (1)
#include <winsock2.h>
#include <windows.h>
#pragma comment(lib, "ws2_32.lib")
#endif
 /* <DESC>
 * Multiplexed HTTP/2 downloads over a single connection
 * </DESC>
 */ 
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/stat.h>

// somewhat unix-specific
#if DEBUG && !defined _WIN32
#include <sys/time.h>
#include <unistd.h>
#endif
 
// curl stuff

//#ifndef STANDALONE_TEST
//#include <curl/curl.h>
//#else

#ifdef _WIN32
#define CURL_STATICLIB (1)
#endif

#include <curl/curl.h>
#ifdef _WIN32
#pragma comment(lib, "lib/libeay32.lib")
#pragma comment(lib, "lib/ssleay32.lib")
#pragma comment(lib, "lib/zlib_a.lib")
#pragma comment(lib, "lib/libcurl_a.lib")
#pragma comment(lib, "lib/nghttp2_static.lib")

#pragma comment(lib, "wldap32.lib")
#pragma comment(lib, "advapi32.lib")
#pragma comment(lib, "crypt32.lib")
#pragma comment(lib, "Normaliz.lib")

#endif
//#endif // STANDALONE_TEST

#ifndef _WIN32
#include <pthread.h>
#else
typedef HANDLE pthread_t;
typedef CRITICAL_SECTION pthread_mutex_t;
#endif
#include <string>
#include <vector>
#include <unordered_map>
#include <unordered_set>
#include <queue>
#include <list>
#include <set>
#include <thread>
#include <cstdio>

//#include "UnityCaller.h"

#ifndef CURLPIPE_MULTIPLEX
// This little trick will just make sure that we don't enable pipelining for
//   libcurls old enough to not have this symbol. It is _not_ defined to zero in
//   a recent libcurl header.
#define CURLPIPE_MULTIPLEX 0
#endif

typedef int proxyresolverFunc(const char* url, char* proxyBuf, int proxyBufSize);

struct dlinfo;
int next;

typedef struct transfer {
    struct dlinfo* info;
    CURL *easy;
    std::string url;
    unsigned int tid;
    int owner_id;
    FILE *out;
    size_t dl_bytes_received;

    transfer(dlinfo* info) {
        this->info = info;
        this->easy = NULL;
        this->tid = next++;
        this->owner_id = 0;
        this->out = NULL;
        this->dl_bytes_received = 0;
    }

    transfer(const struct transfer& o) {
        this->info = o.info;
        this->easy = o.easy;
        this->url = o.url;
        this->tid = o.tid;
        this->owner_id = o.owner_id;
        this->out = o.out;
        this->dl_bytes_received = o.dl_bytes_received;
    }
} transfer;
 
//#define NUM_HANDLES 1000

//void dump(const char *text, int num, unsigned char *ptr, size_t size, char nohex);

static int my_trace(CURL *handle, curl_infotype type,
             char *data, size_t size,
                    void *userp);

static int setup(transfer *t, const std::string& url, int owner_id, const char* proxy);
static void teardown(transfer* pt, const std::list<int>& ids, CURLcode result, int http_code);
void mkdirR(const std::string& dir, size_t ofs = 0);

#define DEFAULT_MAX_CONNECTION (128)

typedef struct request_queue_entry {
    int id;
    std::string url;
    
    request_queue_entry(int id, const std::string& url) {
        this->id = id;
        this->url = url;
    }
} request_queue_entry;

typedef struct response_queue_entry {
    int id;
    int result;
    int http_code;
    int owner_id;
    std::string url;

    response_queue_entry(int id, int result, int http_code, int owner_id, const std::string& url) {
        this->id = id;
        this->result = result;
        this->http_code = http_code;
        this->owner_id = owner_id;
        this->url = url;
    }
} response_queue_entry;

#ifndef _WIN32
#define INVALID_HANDLE_VALUE (0)
#endif

typedef struct dlinfo {
    pthread_t pt;
    pthread_mutex_t mt;
    int http_state; // 0:disconnect, 1:connecting, 10:http1, 20:http2
    proxyresolverFunc* proxyresolver;
    int maxId;
    CURLM* multi_handle;
    std::string sourceAssetBundleURL;
    std::string tmpDir;
    bool isSecure;
    std::unordered_map<CURL*, transfer*> trans;
    std::queue<request_queue_entry*> request_queue;
    std::unordered_map<std::string, std::list<int> > exec_map;
    std::unordered_map<int, transfer*> exec_index;
    std::queue<response_queue_entry*> response_queue;
    int cond;
    int maxConnection;
    int isAnyDeath;

    dlinfo()
        :pt(INVALID_HANDLE_VALUE)
        ,mt()
        ,http_state(0)
        ,proxyresolver(NULL)
        ,maxId(0)
        ,multi_handle(NULL)
        ,sourceAssetBundleURL()
        ,tmpDir()
        ,isSecure(false)
        ,trans()
        ,request_queue()
        ,exec_map()
        ,exec_index()
        ,response_queue()
        ,cond(0)
        ,maxConnection(0)
        ,isAnyDeath(0)
    {
    }
} dlinfo;

#ifdef _WIN32
#define DllExport __declspec(dllexport)
#else
#define DllExport
#endif

extern "C" {

    DllExport int dlboot(const char* sourceAssetBundleURL, const char* tmpDir, proxyresolverFunc* proxyresolver, int maxConnection);
    DllExport int dlshutdown();
    DllExport int dlsetmaxconn(int maxConnection);

    DllExport int dlreq(const char* url);
    DllExport int dlres(int* presult, char* pfilebuf, int len, int* premain);
    DllExport int dlprogress(int id);

}

//extern void UnitySendMessage(const char* obje, const char* method, const char* msg);

int dlentry();

#ifdef _WIN32
DWORD WINAPI dlmain(LPVOID* p);
#else
void* dlmain(void* p);
#endif

//
//Download many transfers over HTTP/2, using the same connection!
//
static dlinfo info;

int dummyProxvResolver(const char* url, char* unused, int unused2) {
    return 0;
}

int main(int argc, char **argv) {
    dlentry();
}

void log(const char* fmt, ...) {
#if DEBUG
    va_list ap;
    char buf[1025];
    time_t timer;
    struct tm tm2;
    struct tm* pt = &tm2;
    time(&timer);
#if _WIN32
    localtime_s(pt, &timer);
#else
    localtime_r(&timer, pt);
#endif
    // "[YYYY-mm-dd HH:ii:ss] "
//    sprintf(buf, "[%04d-%02d-%02d %02d:%02d:%02d] ", pt->tm_year+1900, pt->tm_mon+1, pt->tm_mday, pt->tm_hour, pt->tm_min, pt->tm_sec);
    int idx = 0;
    buf[idx] = '['; idx+=1;
    sprintf(buf+idx, "%04d", pt->tm_year+1900); idx+=4;
    buf[idx] = '-'; idx+=1;
    sprintf(buf+idx, "%02d", pt->tm_mon+1); idx+=2;
    buf[idx] = '-'; idx+=1;
    sprintf(buf+idx, "%02d", pt->tm_mday); idx+=2;
    buf[idx] = ' '; idx+=1;
    sprintf(buf+idx, "%02d", pt->tm_hour); idx+=2;
    buf[idx] = ':'; idx+=1;
    sprintf(buf+idx, "%02d", pt->tm_min); idx+=2;
    buf[idx] = ':'; idx+=1;
    sprintf(buf+idx, "%02d", pt->tm_sec); idx+=2;
    buf[idx] = ']'; idx+=1;
    buf[idx] = ' '; idx+=1;
    va_start(ap, fmt);
    //auto len = vsnprintf(buf+22, 1024-22, fmt, ap);
    auto len = vsnprintf(buf+idx, 1024-idx, fmt, ap);
    va_end(ap);

    buf[idx+len] = '\n';
    buf[idx+len+1] = '\0';

#ifndef STANDALONE_TEST
    //pthread_mutex_lock(&info.mt);
    FILE* fp = fopen((info.tmpDir+"log.txt").c_str(), "a");
#else
    FILE* fp = stdout;
#endif // STANDALONE_TEST
    if (!fp) return;
    fputs(buf, fp);
#ifndef STANDALONE_TEST
    fclose(fp);
    //pthread_mutex_unlock(&info.mt);
#endif // STANDALONE_TEST

#endif // DEBUG
}

#ifdef _WIN32

#endif

int dlboot(const char* sourceAssetBundleURL, const char* tmpDir, proxyresolverFunc *proxyresolver, int maxConnection) {
    if (info.pt != INVALID_HANDLE_VALUE) {
        dlshutdown();
    }
#ifndef _WIN32
    info.mt = PTHREAD_MUTEX_INITIALIZER;
#else
    InitializeCriticalSection(&info.mt);
#endif
    info.proxyresolver = proxyresolver;
    info.cond = 1;
    if (maxConnection <= 0) {
        maxConnection = DEFAULT_MAX_CONNECTION;
    }
    info.sourceAssetBundleURL = sourceAssetBundleURL;
    //info.destAssetBundleDir = destAssetBundleDir;
    info.tmpDir = tmpDir;
    mkdirR(info.tmpDir.c_str());
    info.isSecure = (strstr(sourceAssetBundleURL, "https:") == sourceAssetBundleURL);
    info.maxConnection = maxConnection;
#ifdef _WIN32
    info.pt = CreateThread(0, 0, (LPTHREAD_START_ROUTINE)dlmain, (LPVOID)&info, 0, NULL);
#else
    pthread_create(&info.pt, NULL, &dlmain, &info);
#endif
    return 0;
}

int dlshutdown() {
    if (info.pt == INVALID_HANDLE_VALUE) {
        return 0;
    }
    info.cond = 0;
#ifdef _WIN32
    WaitForSingleObject(info.pt, INFINITE);
    CloseHandle(info.pt);
    info.pt = INVALID_HANDLE_VALUE;
#else
    pthread_join(info.pt, NULL);
    info.pt = NULL;
#endif

#ifdef _WIN32
    DeleteCriticalSection(&info.mt);
#else
    pthread_mutex_destroy(&info.mt);
#endif
    return 0;
}

int dlreq(const char* url) {
    if (!info.pt) { return 0u; }

    info.maxId++;
    auto id = info.maxId;
    auto entry = new request_queue_entry(
        id,
        url
        // ,[=](int result){
        //    log("onend result=%d", result);
        //    char msg[512];
        //    sprintf(msg, "{\"id\":%d,\"r\":%d}", id, result);
        //    return 0;
        //}
    );
    info.request_queue.push(entry);
    return id;
}

int dlprogress(int id) {
    if (!info.pt) { return 0; }

    int progress = 0;
    //pthread_mutex_lock(&info.mt);
    auto it = info.exec_index.find(id);
    if (it != info.exec_index.end()) {
        progress = (int)it->second->dl_bytes_received;
    }
    //pthread_mutex_unlock(&info.mt);
    return progress;
}

#ifdef _WIN32
inline void pthread_mutex_lock(pthread_mutex_t* pt) {
    EnterCriticalSection(pt);
}
inline void pthread_mutex_unlock(pthread_mutex_t* pt) {
    LeaveCriticalSection(pt);
}
#endif

int dlres(int* presult, char* pfilebuf, int len, int* premain) {
    int size = (int)info.response_queue.size();
    if (!size) {
        return 0;
    }
    log("dlres_q_lock");
    pthread_mutex_lock(&info.mt); // dlshutdown直後にdlres呼ばれると同時アクセスする可能性がある.
    size = (int)info.response_queue.size();
    if (!size) {
        // 念の為
        //log("already empty size=%d", size);
        pthread_mutex_unlock(&info.mt);
        log("dlres_q_lock-abort");
        return 0;
    }
    auto entry = info.response_queue.front();
    info.response_queue.pop();
    size--;
    //log("poped size=%d", size);
    pthread_mutex_unlock(&info.mt);
    log("dlres_q_unlock");
    auto id = entry->id;
    auto result = entry->result;
    auto http_code = entry->http_code;
    auto owner_id = entry->owner_id;
    delete entry;

    if (presult) {
        int mixed_result = 0;
        if (result) {
            mixed_result = result * 10000 + http_code;
        }
        *presult = mixed_result;
    }
    if (pfilebuf && 0 < len) {
        if (!result) {
            sprintf(pfilebuf, "tmp_%x", owner_id);
        } else {
            pfilebuf[0] = '\0';
        }
    }
    if (premain) {
        *premain = size;
    }
    log("dlres=%d", id);
    return id;
}

int dlsetmaxconn(int maxConnection) {
    if (maxConnection <= 0) {
        maxConnection = DEFAULT_MAX_CONNECTION;
    }
    info.maxConnection = maxConnection;
    return 0;
}

int dlentry() {
#ifdef STANDALONE_TEST
    dlboot("https://simpleh2downloader.web.app/AssetBundles/", "/tmp/", &dummyProxvResolver, 250);
    while (info.cond) {
        log("input any key...");
        rewind(stdin);
        char c = getchar();
        switch (c) {
            case 'a':
                dlreq("Android/Android");
                break;
            case 'q':
                dlshutdown();
                break;
            default: break;
        }
    }
#endif
    return 0;
}

#ifdef _WIN32
DWORD WINAPI dlmain(LPVOID* p) {
#else
void* dlmain(void* p) {
#endif
    dlinfo* info = (dlinfo*)p;
    //int i;
    int still_running = 0; // keep number of running handles

//    mkdirR(info->destAssetBundleDir.c_str());

    // init a multi stack
    info->multi_handle = curl_multi_init();
 
    curl_multi_setopt(info->multi_handle, CURLMOPT_PIPELINING, CURLPIPE_MULTIPLEX);
    curl_multi_setopt(info->multi_handle, CURLMOPT_MAXCONNECTS, DEFAULT_MAX_CONNECTION);
#ifndef STANDALONE_TEST
    curl_multi_setopt(info->multi_handle, CURLMOPT_MAX_CONCURRENT_STREAMS, DEFAULT_MAX_CONNECTION);
#endif
    // we start some action by calling perform right away
    curl_multi_perform(info->multi_handle, &still_running);

    char proxy[256];
    proxy[0] = '\0';
    while(info->cond) {
        //log("secure=%s http_state=%d", (info->isSecure?"true":"false"), info->http_state);
        struct timeval timeout;
        int rc; // select() return code
        CURLMcode mc; // curl_multi_fdset() return code
 
        fd_set fdread;
        fd_set fdwrite;
        fd_set fdexcep;
        int maxfd = -1;
 
        long curl_timeo = -1;
 
        FD_ZERO(&fdread);
        FD_ZERO(&fdwrite);
        FD_ZERO(&fdexcep);
 
        // set a suitable timeout to play around with
        timeout.tv_sec = 1;
        timeout.tv_usec = 0;
 
        curl_multi_timeout(info->multi_handle, &curl_timeo);
        if(curl_timeo >= 0) {
            timeout.tv_sec = curl_timeo / 1000;
            if(timeout.tv_sec > 1)
                timeout.tv_sec = 1;
            else
                timeout.tv_usec = (curl_timeo % 1000) * 1000;
        }
 
        // get file descriptors from the transfers
        mc = curl_multi_fdset(info->multi_handle, &fdread, &fdwrite, &fdexcep, &maxfd);
 
        if(mc != CURLM_OK) {
            log("[ERROR] curl_multi_fdset() failed, code %d.", mc);
            break;
        }
 
        // On success the value of maxfd is guaranteed to be >= -1. We call
        // select(maxfd + 1, ...); specially in case of (maxfd == -1) there are
        // no fds ready yet so we call select(0, ...) --or Sleep() on Windows--
        // to sleep 100ms, which is the minimum suggested value in the
        // curl_multi_fdset() doc.
 
        if(maxfd == -1) {
#ifdef _WIN32
            Sleep(100);
            rc = 0;
#else
            // Portable sleep for platforms other than Windows.
            struct timeval wait = { 0, 100 * 1000 }; // 100ms
            rc = select(0, NULL, NULL, NULL, &wait);
#endif
        }
        else {
            // Note that on some platforms 'timeout' may be modified by select().
            // If you need access to the original value save a copy beforehand.
            rc = select(maxfd + 1, &fdread, &fdwrite, &fdexcep, &timeout);
        }
 
        int msgs_in_queue = 0;
        CURLMsg* msg;
        switch(rc) {
            case -1:
                // select error
                break;
            case 0:
            default:
                // timeout or readable/writable sockets
                do {
                    msg = curl_multi_info_read(info->multi_handle, &msgs_in_queue);
                    if (msg && (int)msg->msg == CURLMSG_DONE) {
                        auto result = msg->data.result;
                        log("result=%d", result);

                        std::string url;
                        //std::string fpath;
                        {
                            auto pt = info->trans[msg->easy_handle];
                            url = pt->url;
                            int http_code = 0;
                            {
                                if (CURLE_OK == curl_easy_getinfo(pt->easy, CURLINFO_RESPONSE_CODE, &http_code)) {
                                    if (result != CURLE_HTTP_RETURNED_ERROR && http_code != 200) {
                                        result = CURLE_HTTP_RETURNED_ERROR;
                                    }
                                }
                            }
                            std::list<int>& exec_q = info->exec_map[url];
                            
                            //pthread_mutex_lock(&info->mt);
                            std::list<int> ids;
                            for (auto it = exec_q.cbegin(); it != exec_q.cend(); it++) {
                                auto id = *it;
                                ids.push_back(id);
                                info->exec_index.erase(id);
                            }
                            //pthread_mutex_unlock(&info->mt);
                            info->trans.erase(pt->easy);
                            curl_multi_remove_handle(info->multi_handle, pt->easy);
                            curl_easy_cleanup(pt->easy);
                            teardown(pt, ids, result, http_code);
                            //fpath = pt->fpath;
                            delete pt;
                        }
                        info->exec_map.erase(url);

                        //pthread_mutex_lock(&info->mt); // キューへの挿入はここだけなのでロック不要
                        //auto tmp_res_q = std::vector<response_queue_entry*>();

                        //while (exec_q.size()) {
                        //    auto id = exec_q.front();
                        //    exec_q.pop_front();
                        //    auto entry = new response_queue_entry(id, result, url, fpath);
                        //    info->response_queue.push(entry);
                        //}
                        
                        //pthread_mutex_unlock(&info->mt);
                    }
                } while(msgs_in_queue);
                
                while (!info->request_queue.empty()) {
                    if (info->http_state == 1 || (size_t)info->maxConnection <= info->exec_map.size()) {
                        break;
                    }
                    if (!info->http_state) {
                        info->http_state = 1;
                        proxy[0] = '\0';
                        if (info->proxyresolver) {
                            int proxylen = info->proxyresolver(info->sourceAssetBundleURL.c_str(), proxy, sizeof(proxy)-1);
                            proxy[proxylen] = '\0';
                            if (proxylen) {
                                log("proxy=%s", proxy);
                            }
                        }
                        if (!info->isSecure) {
                            // http1.1モードに切り替え
                            info->maxConnection = (6 < info->maxConnection) ? 6 : info->maxConnection;
                            info->http_state = 10;
                        }
                    }
                    std::string url;
                    int id;
                    //std::function<int(int)> onend;
                    {
                        const auto entry = info->request_queue.front();
                        info->request_queue.pop();
                        url = entry->url;
                        id = entry->id;
//                        onend = entry->onend;
                        delete entry;
                    }

                    auto& exec_q = info->exec_map[url];
                    //log("size=%d", exec_q.size());
                    transfer* pt;
                    bool isFail = false;
                    if (!exec_q.size()) {
                        pt = new transfer(info);
                        //setup(pt, "http2/tiles_final/tile_0.png");
                        if (setup(pt, url, id, proxy)) {
                            isFail = true;
                        }
                        else {
                            // add the individual transfer
                            curl_multi_add_handle(info->multi_handle, pt->easy);
                            info->trans[pt->easy] = pt;

                            //pthread_mutex_lock(&info->mt);
                            info->exec_index[id] = pt;
                            //pthread_mutex_unlock(&info->mt);
                        }
                    } else {
                        const auto front_id = exec_q.front();
                        pt = info->exec_index[front_id];
                    }

                    if (isFail) {
                        auto entry = new response_queue_entry(id, CURLE_WRITE_ERROR, 0, 0, url);
                        info->response_queue.push(entry);
                    }
                    else {
                        exec_q.push_back(id);
                        //pthread_mutex_lock(&info->mt);
                        info->exec_index[id] = pt;
                        //pthread_mutex_unlock(&info->mt);
                    }
                }
                curl_multi_perform(info->multi_handle, &still_running);
                if (still_running == 0 && info->http_state) {
                    info->http_state = 0;
                }
                break;
        }
    }

    while (info->request_queue.size()) {
        auto entry = info->request_queue.front();
        info->request_queue.pop();
        delete entry;
    }
    log("info->request_queue.size()=%d", info->request_queue.size());
    info->exec_map.clear();
    log("info->exec_map.size()=%d", info->exec_map.size());
    info->exec_index.clear();
    log("info->exec_index.size()=%d", info->exec_index.size());
    pthread_mutex_lock(&info->mt);
    while (info->response_queue.size()) {
        auto entry = info->response_queue.front();
        info->response_queue.pop();
        delete entry;
    }
    pthread_mutex_unlock(&info->mt);
    log("info->response_queue.size()=%d", info->response_queue.size());
    info->http_state = 0;

    for(auto it = info->trans.begin(); it != info->trans.end(); it++) {
        auto pt = it->second;
        curl_multi_remove_handle(info->multi_handle, pt->easy);
        curl_easy_cleanup(pt->easy);
        teardown(pt, std::list<int>(), CURLE_ABORTED_BY_CALLBACK, 0);
        delete pt;
    }
    info->trans.clear();
 
    curl_multi_cleanup(info->multi_handle);
     
    return 0;
}

size_t my_fwrite(void* buf, size_t size, size_t nmemb, void* stream) {
    transfer* t = (transfer*)stream;

    double dl_bytes_received = 0.0;
    curl_easy_getinfo(t->easy, CURLINFO_SIZE_DOWNLOAD, &dl_bytes_received);
    t->dl_bytes_received = (size_t)dl_bytes_received;
    //double dl_bytes_remaining = 0.0;
    //curl_easy_getinfo(t->easy, CURLINFO_CONTENT_LENGTH_DOWNLOAD, &dl_bytes_remaining);
    //log("== %u Info: %f / %f", t->tid, dl_bytes_received, dl_bytes_remaining);

    size_t ret = size * nmemb;
    FILE* fout = t->out;
    fwrite(buf, size, nmemb, fout);
    return ret;
}

static int setup(transfer *t, const std::string& url, int owner_id, const char* proxy) {
//    char filename[128];
    CURL *hnd;
 
//    char tmpnam[L_tmpnam];
//    std::tmpnam(tmpnam);
    char tmpnam[256];
    sprintf(tmpnam, "%stmp_%x", t->info->tmpDir.c_str(), owner_id);
    //t->num = num;
    t->owner_id = owner_id;
    t->url = url;
    t->out = fopen(tmpnam, "wb");
    if (!t->out) {
        return -1;
    }
    hnd = t->easy = curl_easy_init();

    // write to this file
//    curl_easy_setopt(hnd, CURLOPT_WRITEDATA, t->out);
    curl_easy_setopt(hnd, CURLOPT_WRITEDATA, t);
    curl_easy_setopt(hnd, CURLOPT_WRITEFUNCTION, my_fwrite);
    curl_easy_setopt(hnd, CURLOPT_FAILONERROR, 1L);
    
    // set the same URL
    curl_easy_setopt(hnd, CURLOPT_URL, (t->info->sourceAssetBundleURL + url).c_str());
    if (proxy[0] != '\0') {
        curl_easy_setopt(hnd, CURLOPT_PROXY, proxy);
    }
 
    // please be verbose
    curl_easy_setopt(hnd, CURLOPT_VERBOSE, 1L);
    
    curl_easy_setopt(hnd, CURLOPT_DEBUGFUNCTION, my_trace);
    curl_easy_setopt(hnd, CURLOPT_DEBUGDATA, t);

    // 接続タイムアウト
    curl_easy_setopt(hnd, CURLOPT_CONNECTTIMEOUT, 10);
    // 4秒の間、秒間1byteしかDLできないときは読み込みタイムアウト.
    curl_easy_setopt(hnd, CURLOPT_LOW_SPEED_LIMIT, 1);
    curl_easy_setopt(hnd, CURLOPT_LOW_SPEED_TIME, 4);
    
    //curl_easy_setopt(hnd, CURLOPT_BUFFERSIZE, CURL_MAX_READ_SIZE);
 
    if (t->info->isSecure) {
        // HTTP/2 please
        curl_easy_setopt(hnd, CURLOPT_HTTP_VERSION, CURL_HTTP_VERSION_2_0);
 
        // we use a self-signed test server, skip verification during debugging
        curl_easy_setopt(hnd, CURLOPT_SSL_VERIFYPEER, 0L);
        curl_easy_setopt(hnd, CURLOPT_SSL_VERIFYHOST, 0L);
    }

#if (CURLPIPE_MULTIPLEX > 0)
    // wait for pipe connection to confirm
    curl_easy_setopt(hnd, CURLOPT_PIPEWAIT, 1L);
#endif
    return 0;
}

static void teardown(transfer* pt, const std::list<int>& ids, CURLcode result, int http_code) {
    auto pinfo = pt->info;

    int p = result;
    if (p != 0) {
        auto t_out = pt->out;
        auto owner_id = pt->owner_id;
        {
            {
                fclose(t_out);
                char tmpnam[256];
                sprintf(tmpnam, "%stmp_%x", pt->info->tmpDir.c_str(), owner_id);
                remove(tmpnam);
            }
        }
        
        for (auto it = ids.cbegin(); it != ids.cend(); it++) {
            auto id = *it;
            auto entry = new response_queue_entry(id, result, http_code, 0, pt->url);
            pinfo->response_queue.push(entry);
            log("res_pushed! to=%d result=%d", id, result);
        }
        
        log("[ERROR] fail-end");
        
        return;
    }

    auto t_url = pt->url;
    auto t_out = pt->out;
    auto t_owner_id = pt->owner_id;
    {
        {
            fclose(t_out);

            log("res_q_lock");
            pthread_mutex_lock(&pinfo->mt);
            for (auto it = ids.cbegin(); it != ids.cend(); it++) {
                auto id = *it;
                auto entry = new response_queue_entry(id, result, http_code, t_owner_id, t_url);
                pinfo->response_queue.push(entry);
                log("res_pushed! to=%d result=%d", id, result);
            }
            pthread_mutex_unlock(&pinfo->mt);
            log("res_q_unlock");
        }
    }
    return;
}

static int my_trace(CURL *handle, curl_infotype type, char *data, size_t size, void *userp)
{
    const char *text;
    struct transfer *t = (struct transfer *)userp;
    unsigned int tid = t->tid;
    (void)handle; // prevent compiler warning
 
    switch(type) {
        case CURLINFO_TEXT:
            log("== %u Info: %s", tid, data);
            if (info.http_state == 10 || info.http_state == 20) {
                // already connecting.
            }
            else if (strstr(data, "Connection state changed (HTTP/2")) {
                // -> h2
                if (info.http_state == 1) {
                    info.http_state = 20;
                }
            }
            else if (strstr(data, "server accepted to use http/1")) {
                // -> h1
                if (info.http_state == 1) {
                    info.maxConnection = (6 < info.maxConnection) ? 6 : info.maxConnection;
                    info.http_state = 10;
                }
            }
            // FALLTHROUGH
        default: // in case a new one is introduced to shock us
            return 0;
 
        case CURLINFO_HEADER_OUT:
            text = "=> Send header";
//            if (!info.isSecure) {
                // 平文でhttp2upgradeが来ないまま CURLINFO_HEADER_OUT が呼ばれた.
                if (info.http_state == 1) {
                    // http1.1モードに切り替え.
                    info.maxConnection = (6 < info.maxConnection) ? 6 : info.maxConnection;
                    info.http_state = 10;
                }
//            }
            break;
        case CURLINFO_DATA_OUT:
            text = "=> Send data";
            break;
        case CURLINFO_SSL_DATA_OUT:
            text = "=> Send SSL data";
            break;
        case CURLINFO_HEADER_IN:
            text = "<= Recv header";
            break;
        case CURLINFO_DATA_IN:
            text = "<= Recv data";
            break;
        case CURLINFO_SSL_DATA_IN:
            text = "<= Recv SSL data";
            break;
    }
 
    //(text, num, (unsigned char *)data, size, 1);
    return 0;
}

#ifdef _WIN32
void mkdir(const char* str, int _unused) {
    CreateDirectory(str, NULL);
}
#endif

void mkdirR(const std::string& dir, size_t ofs) {
    auto nxt = dir.find_first_of("/", ofs, 1);
    if (nxt == std::string::npos) {
        FILE* fp = fopen(dir.c_str(), "r");
        if (fp) {
            fclose(fp);
        } else {
            mkdir(dir.c_str(), 0755);
        }
        return;
    }
    {
        if (0 < nxt) {
            auto dir2 = dir.substr(0, nxt);
            FILE* fp = fopen(dir2.c_str(), "r");
            if (fp) {
                fclose(fp);
            } else {
                mkdir(dir2.c_str(), 0755);
            }
        }
    }
    mkdirR(dir, nxt+1);
}


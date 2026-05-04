#pragma once

#if defined(_WIN32) && defined(BUILDING_PATCHIFY_C)
#define PATCHIFY_C_API __declspec(dllexport)
#elif defined(_WIN32)
#define PATCHIFY_C_API __declspec(dllimport)
#else
#define PATCHIFY_C_API
#endif

#ifdef __cplusplus
extern "C" {
#endif

PATCHIFY_C_API int patchify_process_image(const char* imagePath);
PATCHIFY_C_API int patchify_process_folder(const char* folderPath);

#ifdef __cplusplus
}
#endif
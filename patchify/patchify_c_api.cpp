#include "patchify_c_api.h"

#include "patchify.h"

#include <exception>

int patchify_process_image(const char* imagePath)
{
    try {
        if (imagePath == nullptr || *imagePath == '\0') {
            return 1;
        }

        processImage(imagePath);
        return 0;
    } catch (const std::exception&) {
        return 1;
    }
}

int patchify_process_folder(const char* folderPath)
{
    try {
        if (folderPath == nullptr || *folderPath == '\0') {
            return 1;
        }

        processImageFolder(folderPath);
        return 0;
    } catch (const std::exception&) {
        return 1;
    }
}
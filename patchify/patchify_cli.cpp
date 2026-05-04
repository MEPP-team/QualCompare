#include "patchify_c_api.h"

#include <filesystem>
#include <iostream>
#include <string>

namespace fs = std::filesystem;

static void PrintUsage(const char* executableName)
{
    std::cerr << "Usage: " << executableName << " <image-or-folder> [--folder|--image]" << std::endl;
}

int main(int argc, char** argv)
{
    if (argc < 2) {
        PrintUsage(argv[0]);
        return 1;
    }

    fs::path inputPath = argv[1];
    bool forceFolder = false;
    bool forceImage = false;

    for (int i = 2; i < argc; ++i) {
        const std::string argument = argv[i];
        if (argument == "--folder") {
            forceFolder = true;
        } else if (argument == "--image") {
            forceImage = true;
        } else if (argument == "--help" || argument == "-h") {
            PrintUsage(argv[0]);
            return 0;
        } else {
            std::cerr << "Unknown argument: " << argument << std::endl;
            PrintUsage(argv[0]);
            return 1;
        }
    }

    try {
        if (forceFolder || (!forceImage && fs::is_directory(inputPath))) {
            return patchify_process_folder(inputPath.string().c_str());
        } else {
            return patchify_process_image(inputPath.string().c_str());
        }
    } catch (const std::exception& exception) {
        std::cerr << "Patchify failed: " << exception.what() << std::endl;
        return 1;
    }

    return 0;
}
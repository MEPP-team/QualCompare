#pragma once

#include <string>
#include "../patchify/patchify.h"  // Inclure le fichier de Patchify

//using namespace System;

namespace PatchifyLib {
    public ref class PatchifyWrapper
    {
    public:
        static void ProcessImage(System::String^ imagePath);  // Processing single image
		static void ProcessImageFolder(System::String^ folderPath);  // Processing multiple images in the same folder
    };
}
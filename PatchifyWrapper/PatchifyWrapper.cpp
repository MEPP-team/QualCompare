// PatchifyWrapper.cpp
#include "pch.h"

#include "PatchifyWrapper.h"
#include <msclr/marshal_cppstd.h>  // conversion from std::string to System::String^

using namespace PatchifyLib;
void PatchifyWrapper::ProcessImage(System::String^ imagePath) {
    std::cout << "PatchifyWrapper::ProcessImage called!" << std::endl;
    std::string nativePath = msclr::interop::marshal_as<std::string>(imagePath);
    std::cout << "Converted path: " << nativePath << std::endl;

    processImage(nativePath);  // Calling native function
}
void PatchifyWrapper::ProcessImageFolder(System::String^ folderPath) {
    std::cout << "PatchifyWrapper::ProcessImageFolder called!" << std::endl;
    std::string nativePath = msclr::interop::marshal_as<std::string>(folderPath);
    std::cout << "Converted path: " << nativePath << std::endl;
    
	processImageFolder(nativePath);  // Calling native function
}
// patchify.h : fichier Include pour les fichiers Include système standard,
// ou les fichiers Include spécifiques aux projets.
#include <iostream>
#include <string>
#include <vector>
#include <opencv2/opencv.hpp>
#include <opencv2/core.hpp>
#include <fstream>

#include <sys/stat.h>  // Pour créer un dossier sous Linux/MacOS
#if defined(_WIN32)
	#include <direct.h>
	#define mkdir(x) _mkdir(x)
#else
	#include <sys/stat.h>
	#include <sys/types.h>
#endif
#pragma once

void processImage(std::string imagePath);
void processImageFolder(std::string folderPath);

#include "patchify.h"

#include <algorithm>
#include <cctype>
#include <cstdio>
#include <cstdlib>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <map>
#include <regex>
#include <set>
#include <sstream>
#include <system_error>
#include <vector>

#include <opencv2/core.hpp>
#include <opencv2/imgcodecs.hpp>
#include <opencv2/imgproc.hpp>

using namespace std;
using namespace cv;
namespace fs = std::filesystem;

static const bool WRITE_DEBUG_OVERLAY = false;

// Fonction pour extraire le nom du fichier à partir du chemin complet
string getFileName(const string& filePath) {
	// On prend le nom situé après l'avant-avant dernier slash :
	// ex de l'adresse d'une image: 
	size_t pos = filePath.find_last_of("/");
	string filename = filePath.substr(0, pos);
	pos = filename.find_last_of("/");
	filename = filename.substr(0, pos);
	pos = filename.find_last_of("/");
	filename = filename.substr(pos + 1);
	return filename;
}

vector<string> getFolderName(const string& folderName) {
    // On prend le nom situé après l'avant-avant-avant dernier slash :
    // ex de l'adresse d'une image: 
    // On renvoie le nom du dossier et le path qu'il se trouve devant
	vector<string> result;
    string path;
    size_t pos = folderName.find_last_of("/");
	string foldername = folderName.substr(0, pos);
	pos = foldername.find_last_of("/");
	foldername = foldername.substr(0, pos);
	pos = foldername.find_last_of("/");
	foldername = foldername.substr(0, pos);
	pos = foldername.find_last_of("/");
	foldername = foldername.substr(pos + 1);
	path = folderName.substr(0, pos + 1); // On garde le path jusqu'au dossier
    result.push_back(foldername);
	result.push_back(path);
    return result;
}
string getViewNumber(const string& filePath) {
	// On prend le nombre situé après le dernier underscore et avant le dernier point
	size_t posdot = filePath.find_last_of(".");
	size_t posunderscore = filePath.find_last_of("_");
	return (posdot == string::npos) ? filePath : filePath.substr(posunderscore + 1, posdot - posunderscore - 1);
}
string getMaskPath(const string& imagePath) {
	// L'image se situe dans ./Images/Objet/views/view_x.png
	// Le masque se situe dans ./Images/Objet/masks/mask_x.png
	string maskPath = imagePath;
	size_t pos = maskPath.find_last_of("/");
	maskPath = maskPath.substr(0, pos);
	pos = maskPath.find_last_of("/");
	maskPath = maskPath.substr(0, pos);
	maskPath += "/masks/mask_" + getViewNumber(imagePath) + ".png";
	return maskPath;
}
bool createDirectoryRecursively(const std::string& dirPath) {
    if (dirPath.empty() || dirPath == "/") return true;

    const fs::path targetPath(dirPath);
    std::error_code ec;
    if (fs::create_directories(targetPath, ec) || fs::exists(targetPath)) {
        std::cout << "Successfully created directory: " << targetPath.string() << std::endl;
        return true;
    }

    std::cerr << "Erreur lors de la création du répertoire: " << targetPath.string();
    if (ec) {
        std::cerr << " (" << ec.message() << ")";
    }
    std::cerr << std::endl;
    return false;
}

//string getProjectRoot(const string& filePath) {
//	// On récupère la racine du projet : les images sont situées dans le dossier 
//	size_t pos = filePath.find_last_of("/");
//	string projectDir = filePath.substr(0, pos);
//	pos = projectDir.find_last_of("/");
//	projectDir = projectDir.substr(0, pos);
//	pos = projectDir.find_last_of("/");
//	projectDir = projectDir.substr(0, pos);
//	pos = projectDir.find_last_of("/");
//	projectDir = projectDir.substr(0, pos);
//    pos = projectDir.find_last_of("/");
//    projectDir = projectDir.substr(0, pos);
//
//    return projectDir;
//}
// Fonction pour patchifier une image en utilisant une image d'objet et un masque
// Fonction pour lire tout le contenu du fichier CSV
std::vector<std::string> readCSV(const std::string& filename) {
    std::ifstream file(filename);
    std::vector<std::string> lines;
    std::string line;
    while (std::getline(file, line)) {
        lines.push_back(line);
    }
    return lines;
}

void writeCSV(const std::string& filename, const std::vector<std::string>& lines) {
    std::ofstream file(filename);
    for (const auto& line : lines) {
        file << line << std::endl;
    }
	file.close();
}

// Renvoie les positions de départ (0-based) le long d'un axe,
// en garantissant qu'on place un patch qui touche le bord si nécessaire.
static std::vector<int> makeBeginsCoverEdge(int size, int patchSize, int step)
{
    std::vector<int> begins;
    if (size <= 0 || patchSize <= 0 || step <= 0 || patchSize > size) return begins;

    // pas réguliers
    for (int b = 0; b <= size - patchSize; b += step)
        begins.push_back(b);

    // forcer un dernier patch collé au bord si le dernier "b" ne l'atteint pas
    int mustLast = size - patchSize;                // dernière position possible (collée au bord)
    if (begins.empty() || begins.back() != mustLast)
        begins.push_back(mustLast);

    return begins;
}

static std::map<int, int> LoadCountsFromSummary(const std::string& csvSummary)
{
    std::map<int, int> counts; // key: view index, value: nbPatches
    std::ifstream in(csvSummary);
    if (!in.good()) return counts;

    std::string line;
    while (std::getline(in, line))
    {
        // Format attendu: view,<n>,nbPatches,<k>
        // On ignore la 1re ligne de métadonnées (stepX..., object=...)
        if (line.size() >= 5 && line.rfind("view,", 0) == 0)
        {
            // parsing simple sans dépendance
            // tokens: ["view", "<n>", "nbPatches", "<k>"]
            int v = 0, k = 0;
            std::istringstream iss(line);
            std::string tok;
            std::getline(iss, tok, ',');           // view
            std::getline(iss, tok, ',');           // <n>
            v = std::atoi(tok.c_str());
            std::getline(iss, tok, ',');           // nbPatches
            std::getline(iss, tok, ',');           // <k>
            k = std::atoi(tok.c_str());
            if (v > 0) counts[v] = k;
        }
    }
    return counts;
}
static std::string BuildHeaderLine(int px, int py, int patchSize, float overlapThreshold,
    const std::string& objectName,
    const std::map<int, int>& counts)
{
    std::ostringstream oss;
    oss.imbue(std::locale::classic());
    oss << "x, y, "
        << "stepX = " << px << ", "
        << "stepY = " << py << ", "
        << "patchSize = " << patchSize << ", "
        << "overlapThreshold = " << overlapThreshold << ", "
        << "objectName = " << objectName;

    for (const auto& kv : counts)
        oss << ", nbPatchesV" << kv.first << " = " << kv.second;

    oss << "\n"; // 1 ligne d’entête
    return oss.str();
}
void replaceSlash(std::string& str);
static bool LooksLikeDataLine(const std::string& s)
{
    // begins with optional spaces, optional '-', digits, ',', then digits
    size_t i = 0;
    while (i < s.size() && (s[i] == ' ' || s[i] == '\t' || s[i] == '\r')) ++i;
    if (i < s.size() && s[i] == '-') ++i;
    bool hasDigit = false;
    while (i < s.size() && std::isdigit(static_cast<unsigned char>(s[i]))) { hasDigit = true; ++i; }
    if (!hasDigit) return false;
    while (i < s.size() && (s[i] == ' ' || s[i] == '\t')) ++i;
    if (i >= s.size() || s[i] != ',') return false;
    ++i;
    while (i < s.size() && (s[i] == ' ' || s[i] == '\t')) ++i;
    if (i < s.size() && s[i] == '-') ++i;
    hasDigit = false;
    while (i < s.size() && std::isdigit(static_cast<unsigned char>(s[i]))) { hasDigit = true; ++i; }
    return hasDigit;
}

static void RewriteCsvFirstLine(const std::string& csvList, const std::string& newHeader)
{
    const std::string tmp = csvList + ".tmp";

    std::ifstream in(csvList, std::ios::binary);
    std::ofstream out(tmp, std::ios::binary | std::ios::trunc);

    // 1) always write the new header (ends with '\n')
    out.write(newHeader.c_str(), static_cast<std::streamsize>(newHeader.size()));

    if (in.good())
    {
        // 2) read existing first line (header or first data)
        std::string first;
        bool gotFirst = static_cast<bool>(std::getline(in, first));

        // 3) if it looks like data, keep it
        if (gotFirst && LooksLikeDataLine(first))
        {
            if (!first.empty() && first.back() != '\n' && first.back() != '\r') first.push_back('\n');
            out.write(first.c_str(), static_cast<std::streamsize>(first.size()));
        }

        // 4) copy the remainder
        out << in.rdbuf();
    }

    out.flush();
    out.close();
    in.close();

    std::remove(csvList.c_str());
    std::rename(tmp.c_str(), csvList.c_str());
}

// Fonction pour écrire tout le contenu dans le fichier CSV
cv::Mat patchifyImage(const string& imagePath, int patchSize = 64, int px = 32, int py = 32, float overlapThreshold = 0.65) {
    const cv::Mat mask = cv::imread(getMaskPath(imagePath), cv::IMREAD_GRAYSCALE);
    if (mask.empty()) { std::cerr << "Mask missing: " << getMaskPath(imagePath) << std::endl; return cv::Mat(); }
    const int W = mask.cols, H = mask.rows;

    cv::Mat patchifiedImage;
    if (WRITE_DEBUG_OVERLAY) {
        cv::Mat image = cv::imread(imagePath, cv::IMREAD_COLOR);
        if (image.empty()) { std::cerr << "Image missing: " << imagePath << std::endl; return cv::Mat(); }
        patchifiedImage = image.clone();
    }

    // Grille
    std::vector<int> xs = makeBeginsCoverEdge(W, patchSize, px);
    std::vector<int> ys = makeBeginsCoverEdge(H, patchSize, py);

    string filename = getFileName(imagePath);
    string foldername = getFolderName(imagePath)[0];
    string folderPath = getFolderName(imagePath)[1];
    string viewNumber = getViewNumber(imagePath);
    string outputDir = folderPath + foldername + "/" + filename + "/patchs/";

    try { createDirectoryRecursively(outputDir); }
    catch (const std::exception&) { std::cerr << "Echec de la création du répertoire\n"; return patchifiedImage; }

    std::string csvList = outputDir + filename + "_patchlist.csv";
    std::string csvSummary = outputDir + filename + "_summary.csv";

    // -- INITIALISATION PAR OBJET --
    if (viewNumber == "1") {
        // On tronque les fichiers pour un nouvel objet
        std::ofstream(csvList, std::ios::trunc).close();
        std::ofstream(csvSummary, std::ios::trunc)
            << "stepX=" << px << ",stepY=" << py
            << ",patchSize=" << patchSize
            << ",overlap=" << overlapThreshold
            << ",object=" << filename << std::endl;
    }
    else {
        // Si patchlist n’existe pas (exécution partielle), on le crée vide
        if (!std::ifstream(csvList).good())
            std::ofstream(csvList, std::ios::trunc).close();
    }

    // -- APPEND DES PATCHES (x,y) --
    std::ofstream list(csvList, std::ios::app);
    int nbPatches = 0;
    for (int y : ys) {
        for (int x : xs) {
            cv::Mat maskPatch = mask(cv::Rect(x, y, patchSize, patchSize));
            int objectPixels = countNonZero(maskPatch);
            float overlap = static_cast<float>(objectPixels) / (patchSize * patchSize);
            if (overlap >= overlapThreshold) {
                list << x << "," << y << "\n";
                nbPatches++;
                if (WRITE_DEBUG_OVERLAY)
                    rectangle(patchifiedImage, cv::Point(x, y), cv::Point(x + patchSize, y + patchSize), cv::Scalar(0, 255, 0), 2);
            }
        }
    }
    list.close();

    // -- SUMMARY PAR VUE (append-only) --
    {
        std::ofstream sum(csvSummary, std::ios::app);
        sum << "view," << viewNumber << ",nbPatches," << nbPatches << "\n";
    }

    {
        // Charger les compteurs depuis le summary
        std::map<int, int> counts = LoadCountsFromSummary(csvSummary);

        // Construire la 1re ligne complète
        std::string header = BuildHeaderLine(px, py, patchSize, overlapThreshold, filename, counts);

        // Réécrire uniquement la 1re ligne (le reste des x,y est inchangé)
        RewriteCsvFirstLine(csvList, header);
    }

    if (WRITE_DEBUG_OVERLAY) {
        std::string imageOutputPath = outputDir + "view_" + viewNumber + "_patchified.png";
        cv::imwrite(imageOutputPath, patchifiedImage);
    }

    return patchifiedImage;
}
void processImage(std::string imagePath) {
	replaceSlash(imagePath);
    std::cout << "ProcessImage called with the file : " << imagePath << std::endl;
    cv::Mat patchifiedImage = patchifyImage(imagePath);
}
void replaceSlash(std::string& str) {
	for (char& c : str) {
		if (c == '\\') {
			c = '/';
		}
	}
}
void processImageFolder(std::string folderPath) {
    // We are patching all the views located in the same folder
	std::cout << "ProcessImageFolder called with the folder : " << folderPath << std::endl;
	std::vector<std::string> files;
    std::set<std::string> validExtensions = { ".png", ".jpg", ".jpeg", ".bmp", ".tiff" };

    try {
        for (auto& p : std::filesystem::recursive_directory_iterator(folderPath)) {
            // The iterator is incrementing alphabetically...

            // From the this, we take only images located in a "views" folder, including the parents folder. 
            // could totally want to patchify only an object starting from views folder

            if (p.path().string().find("views") != std::string::npos && validExtensions.contains(p.path().extension().string())) {
                // Found image is located somewhere in a views folder
                // Exclude the folder itself !!!
                // p.path().extension() returns "" if it's a folder

                files.push_back(p.path().string());
			    //std::cout << "Entry found : " << p.path().string() << std::endl;
            }
        }
		// !!! WARNING : Processing in alphabetical order : 1, 10, 11, ..., 19, 2, 3, ... !!!
		// We need to sort the files in order to not process them in alphabetical order
        auto numericSort = [](const std::string& a, const std::string& b) {
            std::regex re("(\\d+)");
            std::smatch ma, mb;
            std::string sa = std::filesystem::path(a).stem().string();
            std::string sb = std::filesystem::path(b).stem().string();
            if (std::regex_search(sa, ma, re) && std::regex_search(sb, mb, re)) {
                return std::stoi(ma[1]) < std::stoi(mb[1]);
            }
            return sa < sb;
            };
        std::sort(files.begin(), files.end(), numericSort);

		/*std::sort(files.begin(), files.end(), [](const std::string& a, const std::string& b) {
			return (a.length() == b.length()) ? (a < b) : (a.length() < b.length());
			});*/
        for (std::string& file : files) {
			replaceSlash(file);
			std::cout << "Processing image : " << file << std::endl;
            processImage(file);
        }
    }
	catch (const std::filesystem::filesystem_error& e) {
		std::cerr << "Error: " << e.what() << std::endl;
    }
    
}

//int main(int argc, char** argv) {
//    
//    // ----------------------------------------------------------------------------
//    // USE MAIN ONLY FOR DEBUG PURPOSES. (TURN THE PROJECT TO .EXE PROJECT SINCE IT'S A .LIB PROJECT)
//    // THE PROJECT HAS TO BE A .LIB PROJECT SO COMMENT THE FUNCTION AFTER DEBUGGING 
//    // ----------------------------------------------------------------------------
//    processImageFolder("D:\\These\\Projets\\CompareMetrics\\out\\_TMQ_YANA_\\TMQ_REF_YANA_1VP");
//    return 0;
//}
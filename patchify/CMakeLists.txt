cmake_minimum_required(VERSION 3.20)

project(patchify LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

set(_opencv_config_hints)

if(DEFINED ENV{OPENCV_DIR})
    list(APPEND _opencv_config_hints "$ENV{OPENCV_DIR}" "$ENV{OPENCV_DIR}/build")
endif()

list(APPEND _opencv_config_hints
    "C:/Program Files/opencv/build"
    "C:/Program Files (x86)/opencv/build"
    "/usr/lib/x86_64-linux-gnu/cmake/opencv4"
    "/usr/lib/cmake/opencv4"
    "/usr/local/lib/cmake/opencv4"
    "/opt/homebrew/opt/opencv/lib/cmake/opencv4"
)

set(_opencv_valid_hints)
foreach(_hint IN LISTS _opencv_config_hints)
    if(EXISTS "${_hint}/OpenCVConfig.cmake" OR EXISTS "${_hint}/opencv-config.cmake")
        list(APPEND _opencv_valid_hints "${_hint}")
    endif()
endforeach()

find_package(OpenCV QUIET COMPONENTS core imgcodecs imgproc PATHS ${_opencv_valid_hints})

if(NOT OpenCV_FOUND)
    message(FATAL_ERROR
        "OpenCV was not found. Set OPENCV_DIR or OpenCV_DIR to a folder containing OpenCVConfig.cmake, "
        "or install OpenCV with the development files for your platform."
    )
endif()

add_library(patchify STATIC
    patchify.cpp
)

target_include_directories(patchify
    PUBLIC
        ${CMAKE_CURRENT_SOURCE_DIR}
        ${OpenCV_INCLUDE_DIRS}
)

target_link_libraries(patchify
    PUBLIC
        ${OpenCV_LIBS}
)

add_library(patchify_c SHARED
    patchify_c_api.cpp
)

target_compile_definitions(patchify_c
    PRIVATE
        BUILDING_PATCHIFY_C
)

target_link_libraries(patchify_c
    PRIVATE
        patchify
)

set(_opencv_runtime_target "")
if(TARGET OpenCV::opencv_world)
    set(_opencv_runtime_target OpenCV::opencv_world)
elseif(TARGET opencv_world)
    set(_opencv_runtime_target opencv_world)
endif()

if(_opencv_runtime_target)
    add_custom_command(TARGET patchify_c POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E copy_if_different
            $<TARGET_FILE:${_opencv_runtime_target}>
            $<TARGET_FILE_DIR:patchify_c>
        VERBATIM
    )
endif()

add_executable(patchify_cli
    patchify_cli.cpp
)

target_link_libraries(patchify_cli
    PRIVATE
        patchify_c
)

install(TARGETS patchify patchify_c patchify_cli
    RUNTIME DESTINATION bin
    LIBRARY DESTINATION lib
    ARCHIVE DESTINATION lib
)
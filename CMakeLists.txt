cmake_minimum_required(VERSION 3.5)
project(Client)

set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -std=c++11")
set(CURSES_USE_NCURSES TRUE)
find_package(Curses REQUIRED)
include_directories(${CURSES_INCLUDE_DIR})


set(SOURCE_FILES main.cpp)
add_executable(Client ${SOURCE_FILES})

target_link_libraries(Client ${CURSES_LIBRARIES})
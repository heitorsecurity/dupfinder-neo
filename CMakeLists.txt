cmake_minimum_required(VERSION 3.16)
project(dupfinder-neo VERSION 0.9.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

# Option to use wxWidgets or build standalone
find_package(wxWidgets COMPONENTS core base adv QUIET)

# Common sources
set(CORE_SOURCES
    dbl.cpp
    dbl.h
    file.cpp
    file.h
    os_cc_specific.cpp
    os_cc_specific.h
    largeint.h
    ulargeint.h
    dllexport.h
    minmax.h
    profile.h
)

if(wxWidgets_FOUND)
    include(${wxWidgets_USE_FILE})

    # CLI Executable
    add_executable(dupf dupf.cpp ${CORE_SOURCES})
    target_link_libraries(dupf PRIVATE ${wxWidgets_LIBRARIES})

    # GUI Executable
    set(GUI_SOURCES
        dupfgui.cpp
        dupfgui.h
        dialog1.cpp
        dialog1.h
        dialog2.cpp
        dialog2.h
        dialog3.cpp
        dialog3.h
        dialog3_list.cpp
        dialog3_list.h
        treectrl.cpp
        treectrl.h
        os_cc_specific_gui.cpp
        os_cc_specific_gui.h
    )

    if(WIN32)
        list(APPEND GUI_SOURCES rsrc.rc)
    endif()

    add_executable(dupfgui WIN32 ${GUI_SOURCES} ${CORE_SOURCES})
    target_link_libraries(dupfgui PRIVATE ${wxWidgets_LIBRARIES})

    if(WIN32)
        target_link_libraries(dupfgui PRIVATE shlwapi ole32 uuid user32 advapi32 shell32 gdi32 comctl32 comdlg32 oleaut32 rpcrt4)
    endif()
else()
    message(STATUS "wxWidgets not found. Configured for core compilation.")
endif()


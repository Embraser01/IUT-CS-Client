#include <ncurses.h>
#include <iostream>

using namespace std;

int main(int argc, char* argv[]) {
    WINDOW *haut, *bas;

    if(argc < 1) {
        perror("Use : client <adress name>");
        return EXIT_FAILURE;
    }

    initscr();

    haut = subwin(stdscr, LINES/2, COLS,0,0);
    bas = subwin(stdscr, LINES/2, COLS, LINES/2, 0);

    box(haut, ACS_VLINE, ACS_HLINE);
    box(bas, ACS_VLINE, ACS_HLINE);

    mvwprintw(haut, 1,1, "Ceci est en haut");
    mvwprintw(bas, 1,1, "Ceci est en bas");

    wrefresh(haut);
    wrefresh(bas);

    getch();
    endwin();

    return EXIT_SUCCESS;
}
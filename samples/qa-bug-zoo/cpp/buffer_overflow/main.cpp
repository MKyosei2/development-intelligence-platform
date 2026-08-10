#include <cstdlib>
int main(){auto* p=new int[2];p[4]=7;volatile int v=p[4];delete[] p;return v==7?0:1;}

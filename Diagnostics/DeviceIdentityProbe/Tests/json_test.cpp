#include "../Native/json.h"
#include <iostream>
int main() {
    std::string value="quote\" slash\\ newline\n tab\t null";value+='\0';value+=" snowman \xe2\x98\x83";
    std::cout<<"{\"evidenceClass\":\"synthetic\",\"fields\":"<<probe::array({probe::field("test",value,"synthetic"),probe::field("missing","","synthetic"),probe::field("error","","synthetic","error","5")})<<"}";
}

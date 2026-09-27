#pragma once
#include <string>
#include <string_view>
#include <vector>

namespace probe {
inline std::string quote(std::string_view s) {
    constexpr char hex[]="0123456789abcdef";
    std::string out="\"";
    for(unsigned char c:s) {
        if(c=='"'||c=='\\'){out+='\\';out+=char(c);}
        else if(c<32){out+="\\u00";out+=hex[c>>4];out+=hex[c&15];}
        else out+=char(c);
    }
    return out+'"';
}
inline std::string array(const std::vector<std::string>& values) {
    std::string out="[";bool first=true;
    for(const auto& v:values){if(!first)out+=',';first=false;out+=v;}
    return out+']';
}
inline std::string field(const std::string& name,const std::string& value,const std::string& source,
                         const std::string& status="present",const std::string& error="") {
    return "{\"name\":"+quote(name)+",\"value\":"+quote(value)+",\"source\":"+quote(source)+
        ",\"status\":"+quote(status=="present"&&value.empty()?"missing":status)+",\"error\":"+quote(error)+"}";
}
inline std::string error(const std::string& api,unsigned long code) {
    return "{\"api\":"+quote(api)+",\"code\":"+quote(std::to_string(code))+",\"status\":\"error\"}";
}
}

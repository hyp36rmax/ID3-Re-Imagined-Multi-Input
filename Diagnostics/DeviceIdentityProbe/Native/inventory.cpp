// Isolated metadata inventory. No gameplay headers or force-output code is linked.
#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#define DIRECTINPUT_VERSION 0x0800
#include <windows.h>
#include <initguid.h>
#include <setupapi.h>
#include <devpkey.h>
#include <cfgmgr32.h>
#include <hidsdi.h>
#include <hidpi.h>
#include <dinput.h>
#include <xinput.h>
#include <algorithm>
#include <cstdint>
#include <memory>
#include <stdexcept>
#include "json.h"

#ifndef PROBE_BUILD_ID
#define PROBE_BUILD_ID "unidentified-build"
#endif

namespace {
using probe::field;
using Fields=std::vector<std::string>;
std::string utf8(const wchar_t* s) {
    if(!s||!*s)return {};
    int n=WideCharToMultiByte(CP_UTF8,WC_ERR_INVALID_CHARS,s,-1,nullptr,0,nullptr,nullptr);
    if(n<=0)throw std::runtime_error("utf8-conversion");
    std::string value(static_cast<size_t>(n),'\0');
    if(!WideCharToMultiByte(CP_UTF8,WC_ERR_INVALID_CHARS,s,-1,value.data(),n,nullptr,nullptr))throw std::runtime_error("utf8-conversion");
    value.pop_back();return value;
}
std::string guid(const GUID& g) {wchar_t value[40]{};StringFromGUID2(g,value,40);return utf8(value);}
void failed(Fields& f,const char* name,const char* api,unsigned long code) {
    f.push_back(field(name,"",api,code==ERROR_NOT_FOUND?"missing":"error",std::to_string(code)));
}
void number(Fields& f,const char* name,unsigned long value,const char* api) {f.push_back(field(name,std::to_string(value),api));}
struct Handle {HANDLE value=INVALID_HANDLE_VALUE;~Handle(){if(value!=INVALID_HANDLE_VALUE)CloseHandle(value);}};
struct DeviceSet {HDEVINFO value=INVALID_HANDLE_VALUE;~DeviceSet(){if(value!=INVALID_HANDLE_VALUE)SetupDiDestroyDeviceInfoList(value);}};
template<class T> struct Com {T* value=nullptr;~Com(){if(value)value->Release();}};
std::string endpoint(const std::string& id,const char* backend,const Fields& fields,int slot=-1,bool connected=true,
                     const std::vector<std::string>& controls={}) {
    return "{\"id\":"+probe::quote(id)+",\"backend\":"+probe::quote(backend)+",\"slot\":"+std::to_string(slot)+
        ",\"runtimeId\":-1,\"connected\":"+(connected?"true":"false")+",\"fields\":"+probe::array(fields)+
        ",\"controls\":"+probe::array(controls)+"}";
}
std::string sample(const char* path,long value) {
    return "{\"path\":"+probe::quote(path)+",\"layout\":\"native-integer\",\"status\":\"present\",\"value\":"+probe::quote(std::to_string(value))+"}";
}
void property(HDEVINFO set,SP_DEVINFO_DATA& device,const DEVPROPKEY& key,const char* name,Fields& f) {
    DEVPROPTYPE type=0;DWORD size=0;
    if(!SetupDiGetDevicePropertyW(set,&device,&key,&type,nullptr,0,&size,0)&&GetLastError()!=ERROR_INSUFFICIENT_BUFFER){failed(f,name,"SetupDiGetDevicePropertyW",GetLastError());return;}
    if(size==0||size>1024*1024){failed(f,name,"SetupDiGetDevicePropertyW.size",ERROR_INVALID_DATA);return;}
    std::vector<BYTE> bytes(size+sizeof(wchar_t)*2,0);
    if(!SetupDiGetDevicePropertyW(set,&device,&key,&type,bytes.data(),size,nullptr,0)){failed(f,name,"SetupDiGetDevicePropertyW",GetLastError());return;}
    std::string value;
    if(type==DEVPROP_TYPE_GUID&&size==sizeof(GUID)){GUID g{};memcpy(&g,bytes.data(),sizeof g);if(!IsEqualGUID(g,GUID_NULL))value=guid(g);}
    else if(type==DEVPROP_TYPE_STRING||type==DEVPROP_TYPE_STRING_LIST){
        if(size%sizeof(wchar_t)){failed(f,name,"SetupDiGetDevicePropertyW.type",ERROR_INVALID_DATA);return;}
        auto* p=reinterpret_cast<const wchar_t*>(bytes.data());
        if(type==DEVPROP_TYPE_STRING)value=utf8(p);
        else {size_t at=0,limit=size/sizeof(wchar_t);while(at<limit&&p[at]){if(!value.empty())value+='\n';value+=utf8(p+at);at+=wcslen(p+at)+1;}}
    } else {failed(f,name,"SetupDiGetDevicePropertyW.type",ERROR_INVALID_DATATYPE);return;}
    f.push_back(field(name,value,"SetupDiGetDevicePropertyW"));
}
void devnode(HDEVINFO set,SP_DEVINFO_DATA& device,Fields& f) {
    wchar_t id[MAX_DEVICE_ID_LEN]{};
    if(SetupDiGetDeviceInstanceIdW(set,&device,id,MAX_DEVICE_ID_LEN,nullptr))f.push_back(field("instance",utf8(id),"SetupDiGetDeviceInstanceIdW"));
    else failed(f,"instance","SetupDiGetDeviceInstanceIdW",GetLastError());
    DEVINST parent=0;CONFIGRET cr=CM_Get_Parent(&parent,device.DevInst,0);
    if(cr==CR_SUCCESS)cr=CM_Get_Device_IDW(parent,id,MAX_DEVICE_ID_LEN,0);
    if(cr==CR_SUCCESS)f.push_back(field("parent",utf8(id),"CM_Get_Parent/CM_Get_Device_IDW"));
    else failed(f,"parent","CM_Get_Parent/CM_Get_Device_IDW",cr);
    property(set,device,DEVPKEY_Device_ContainerId,"container",f);
    property(set,device,DEVPKEY_Device_LocationPaths,"locations",f);
    // Container provenance cannot be inferred from its GUID. Keep it explicitly unknown.
    f.push_back(field("containerProvenance","","Windows PnP","unknown"));
}
void hidMetadata(const wchar_t* path,Fields& f) {
    // Desired access 0: metadata queries only, shared with Unity and all other readers.
    Handle handle{CreateFileW(path,0,FILE_SHARE_READ|FILE_SHARE_WRITE,nullptr,OPEN_EXISTING,0,nullptr)};
    if(handle.value==INVALID_HANDLE_VALUE){
        DWORD code=GetLastError();for(const char* n:{"serial","vendorId","productId","usagePage","usage"})failed(f,n,"CreateFileW(metadata)",code);return;
    }
    wchar_t serial[256]{};
    if(HidD_GetSerialNumberString(handle.value,serial,sizeof(serial))) {
        serial[255]=0;f.push_back(field("serial",utf8(serial),"HidD_GetSerialNumberString"));
    } else failed(f,"serial","HidD_GetSerialNumberString",GetLastError());
    HIDD_ATTRIBUTES attributes{};attributes.Size=sizeof(attributes);
    if(HidD_GetAttributes(handle.value,&attributes)){number(f,"vendorId",attributes.VendorID,"HidD_GetAttributes");number(f,"productId",attributes.ProductID,"HidD_GetAttributes");}
    else {DWORD code=GetLastError();failed(f,"vendorId","HidD_GetAttributes",code);failed(f,"productId","HidD_GetAttributes",code);}
    PHIDP_PREPARSED_DATA data=nullptr;
    if(!HidD_GetPreparsedData(handle.value,&data)){failed(f,"usagePage","HidD_GetPreparsedData",GetLastError());failed(f,"usage","HidD_GetPreparsedData",GetLastError());return;}
    HIDP_CAPS caps{};NTSTATUS status=HidP_GetCaps(data,&caps);HidD_FreePreparsedData(data);
    if(status!=HIDP_STATUS_SUCCESS){failed(f,"usagePage","HidP_GetCaps",static_cast<ULONG>(status));failed(f,"usage","HidP_GetCaps",static_cast<ULONG>(status));return;}
    number(f,"usagePage",caps.UsagePage,"HidP_GetCaps");number(f,"usage",caps.Usage,"HidP_GetCaps");
    number(f,"inputReportSize",caps.InputReportByteLength,"HidP_GetCaps");
    number(f,"outputReportSize",caps.OutputReportByteLength,"HidP_GetCaps");number(f,"featureReportSize",caps.FeatureReportByteLength,"HidP_GetCaps");
}
void hidInventory(std::vector<std::string>& endpoints,Fields& errors) {
    GUID hid{};HidD_GetHidGuid(&hid);
    DeviceSet set{SetupDiGetClassDevsW(&hid,nullptr,nullptr,DIGCF_PRESENT|DIGCF_DEVICEINTERFACE)};
    if(set.value==INVALID_HANDLE_VALUE){errors.push_back(probe::error("SetupDiGetClassDevsW",GetLastError()));return;}
    for(DWORD index=0;;++index){
        SP_DEVICE_INTERFACE_DATA iface{};iface.cbSize=sizeof(iface);
        if(!SetupDiEnumDeviceInterfaces(set.value,nullptr,&hid,index,&iface)){
            DWORD e=GetLastError();if(e!=ERROR_NO_MORE_ITEMS)errors.push_back(probe::error("SetupDiEnumDeviceInterfaces",e));break;
        }
        DWORD size=0;SetupDiGetDeviceInterfaceDetailW(set.value,&iface,nullptr,0,&size,nullptr);
        if(size<sizeof(SP_DEVICE_INTERFACE_DETAIL_DATA_W)||size>1024*1024){errors.push_back(probe::error("SetupDiGetDeviceInterfaceDetailW.size",GetLastError()));continue;}
        std::vector<BYTE> bytes(size+sizeof(wchar_t),0);auto* detail=reinterpret_cast<SP_DEVICE_INTERFACE_DETAIL_DATA_W*>(bytes.data());detail->cbSize=sizeof(*detail);
        SP_DEVINFO_DATA device{};device.cbSize=sizeof(device);
        if(!SetupDiGetDeviceInterfaceDetailW(set.value,&iface,detail,size,nullptr,&device)){errors.push_back(probe::error("SetupDiGetDeviceInterfaceDetailW",GetLastError()));continue;}
        Fields f;std::string path=utf8(detail->DevicePath);f.push_back(field("path",path,"SetupDiGetDeviceInterfaceDetailW"));
        devnode(set.value,device,f);hidMetadata(detail->DevicePath,f);
        endpoints.push_back(endpoint("hid:"+path,"hid",f));
    }
}
void rawInventory(std::vector<std::string>& endpoints,Fields& errors) {
    UINT count=0;
    if(GetRawInputDeviceList(nullptr,&count,sizeof(RAWINPUTDEVICELIST))==UINT(-1)){errors.push_back(probe::error("GetRawInputDeviceList",GetLastError()));return;}
    // Retry a growing list, but never register for WM_INPUT or replace Unity's registration.
    std::vector<RAWINPUTDEVICELIST> devices;
    UINT got=UINT(-1);
    for(int attempt=0;attempt<3;++attempt){devices.resize(count+8);count=static_cast<UINT>(devices.size());got=GetRawInputDeviceList(devices.data(),&count,sizeof(RAWINPUTDEVICELIST));if(got!=UINT(-1))break;}
    if(got==UINT(-1)){errors.push_back(probe::error("GetRawInputDeviceList",GetLastError()));return;}
    for(UINT i=0;i<got;++i){if(devices[i].dwType!=RIM_TYPEHID)continue;
        HANDLE handle=devices[i].hDevice;Fields f;UINT size=0;
        std::string path;
        if(GetRawInputDeviceInfoW(handle,RIDI_DEVICENAME,nullptr,&size)==UINT(-1))failed(f,"path","GetRawInputDeviceInfoW",GetLastError());
        else if(size>1024*1024)failed(f,"path","GetRawInputDeviceInfoW.size",ERROR_INVALID_DATA);
        else {std::vector<wchar_t> name(size+1,0);if(GetRawInputDeviceInfoW(handle,RIDI_DEVICENAME,name.data(),&size)==UINT(-1))failed(f,"path","GetRawInputDeviceInfoW",GetLastError());else {path=utf8(name.data());f.push_back(field("path",path,"GetRawInputDeviceInfoW(RIDI_DEVICENAME)"));}}
        RID_DEVICE_INFO info{};info.cbSize=sizeof(info);size=sizeof(info);
        if(GetRawInputDeviceInfoW(handle,RIDI_DEVICEINFO,&info,&size)!=UINT(-1)){
            number(f,"vendorId",info.hid.dwVendorId,"GetRawInputDeviceInfoW");number(f,"productId",info.hid.dwProductId,"GetRawInputDeviceInfoW");number(f,"usagePage",info.hid.usUsagePage,"GetRawInputDeviceInfoW");number(f,"usage",info.hid.usUsage,"GetRawInputDeviceInfoW");
        }else failed(f,"capabilities","GetRawInputDeviceInfoW",GetLastError());
        endpoints.push_back(endpoint(path.empty()?"raw-session:"+std::to_string(reinterpret_cast<uintptr_t>(handle)):"raw:"+path,"rawinput",f));
    }
}
struct Enumeration {IDirectInput8W* input;std::vector<std::string>* endpoints;Fields* errors;};
BOOL CALLBACK directDevice(const DIDEVICEINSTANCEW* instance,void* context) noexcept {
    auto& e=*static_cast<Enumeration*>(context);
    try {
        Fields f;f.push_back(field("directInputGuid",guid(instance->guidInstance),"DIDEVICEINSTANCEW.guidInstance"));
        f.push_back(field("productGuid",guid(instance->guidProduct),"DIDEVICEINSTANCEW.guidProduct"));
        f.push_back(field("name",utf8(instance->tszInstanceName),"DIDEVICEINSTANCEW"));
        number(f,"deviceType",instance->dwDevType,"DIDEVICEINSTANCEW");
        Com<IDirectInputDevice8W> device;
        HRESULT hr=e.input->CreateDevice(instance->guidInstance,&device.value,nullptr);
        if(FAILED(hr)){for(const char* n:{"path","ffb","vendorId","productId"})failed(f,n,"IDirectInput8.CreateDevice",static_cast<ULONG>(hr));}
        else {
            DIDEVCAPS caps{};caps.dwSize=sizeof(caps);hr=device.value->GetCapabilities(&caps);
            if(SUCCEEDED(hr)){number(f,"ffb",(caps.dwFlags&DIDC_FORCEFEEDBACK)?1:0,"GetCapabilities");number(f,"capsFlags",caps.dwFlags,"GetCapabilities");number(f,"axes",caps.dwAxes,"GetCapabilities");number(f,"buttons",caps.dwButtons,"GetCapabilities");number(f,"povs",caps.dwPOVs,"GetCapabilities");}
            else failed(f,"ffb","GetCapabilities",static_cast<ULONG>(hr));
            DIPROPGUIDANDPATH path{};path.diph.dwSize=sizeof(path);path.diph.dwHeaderSize=sizeof(path.diph);path.diph.dwHow=DIPH_DEVICE;
            hr=device.value->GetProperty(DIPROP_GUIDANDPATH,&path.diph);
            if(SUCCEEDED(hr)){path.wszPath[MAX_PATH-1]=0;f.push_back(field("path",utf8(path.wszPath),"DIPROP_GUIDANDPATH"));f.push_back(field("classGuid",guid(path.guidClass),"DIPROP_GUIDANDPATH"));}
            else failed(f,"path","DIPROP_GUIDANDPATH",static_cast<ULONG>(hr));
            DIPROPDWORD ids{};ids.diph.dwSize=sizeof(ids);ids.diph.dwHeaderSize=sizeof(ids.diph);ids.diph.dwHow=DIPH_DEVICE;
            hr=device.value->GetProperty(DIPROP_VIDPID,&ids.diph);
            if(SUCCEEDED(hr)){number(f,"vendorId",LOWORD(ids.dwData),"DIPROP_VIDPID");number(f,"productId",HIWORD(ids.dwData),"DIPROP_VIDPID");}
            else {failed(f,"vendorId","DIPROP_VIDPID",static_cast<ULONG>(hr));failed(f,"productId","DIPROP_VIDPID",static_cast<ULONG>(hr));}
        }
        e.endpoints->push_back(endpoint("di:"+guid(instance->guidInstance),"directinput",f));return DIENUM_CONTINUE;
    }catch(...){e.errors->push_back(probe::error("directinput-callback",ERROR_INVALID_DATA));return DIENUM_STOP;}
}
void directInventory(std::vector<std::string>& endpoints,Fields& errors) {
    Com<IDirectInput8W> input;
    HRESULT hr=DirectInput8Create(GetModuleHandleW(nullptr),DIRECTINPUT_VERSION,IID_IDirectInput8W,reinterpret_cast<void**>(&input.value),nullptr);
    if(FAILED(hr)){errors.push_back(probe::error("DirectInput8Create",static_cast<ULONG>(hr)));return;}
    Enumeration context{input.value,&endpoints,&errors};
    hr=input.value->EnumDevices(DI8DEVCLASS_GAMECTRL,directDevice,&context,DIEDFL_ATTACHEDONLY);
    if(FAILED(hr))errors.push_back(probe::error("IDirectInput8.EnumDevices",static_cast<ULONG>(hr)));
}
void xboxInventory(std::vector<std::string>& endpoints) {
    for(DWORD slot=0;slot<4;++slot){Fields f;XINPUT_STATE state{};DWORD result=XInputGetState(slot,&state);std::vector<std::string> controls;
        if(result==ERROR_SUCCESS){
            number(f,"packet",state.dwPacketNumber,"XInputGetState");number(f,"buttonMask",state.Gamepad.wButtons,"XInputGetState");
            controls={sample("leftTrigger",state.Gamepad.bLeftTrigger),sample("rightTrigger",state.Gamepad.bRightTrigger),sample("leftStick/x",state.Gamepad.sThumbLX),sample("leftStick/y",state.Gamepad.sThumbLY),sample("rightStick/x",state.Gamepad.sThumbRX),sample("rightStick/y",state.Gamepad.sThumbRY)};
            XINPUT_CAPABILITIES caps{};DWORD c=XInputGetCapabilities(slot,0,&caps);
            if(c==ERROR_SUCCESS){number(f,"xinputType",caps.Type,"XInputGetCapabilities");number(f,"xinputSubtype",caps.SubType,"XInputGetCapabilities");number(f,"xinputFlags",caps.Flags,"XInputGetCapabilities");}
            else failed(f,"capabilities","XInputGetCapabilities",c);
        } else f.push_back(field("state","","XInputGetState",result==ERROR_DEVICE_NOT_CONNECTED?"disconnected":"error",std::to_string(result)));
        f.push_back(field("serial","","XInputGetState","unsupported"));
        endpoints.push_back(endpoint("xinput-slot:"+std::to_string(slot),"xinput",f,static_cast<int>(slot),result==ERROR_SUCCESS,controls));
    }
}
}

extern "C" __declspec(dllexport) int __cdecl ProbeCapture(const char** data,int* length) noexcept {
    // Called by exactly one managed worker. Returned memory remains valid until its next call.
    static thread_local std::string output;
    if(!data||!length)return 0;
    *data=nullptr;*length=0;
    try {
        std::vector<std::string> endpoints;Fields errors;
        hidInventory(endpoints,errors);rawInventory(endpoints,errors);directInventory(endpoints,errors);xboxInventory(endpoints);
        output="{\"schemaVersion\":1,\"nativeBuild\":"+probe::quote(PROBE_BUILD_ID)+",\"endpoints\":"+probe::array(endpoints)+",\"errors\":"+probe::array(errors)+"}";
        if(output.size()>16*1024*1024)return 0;
        *data=output.data();*length=static_cast<int>(output.size());return 1;
    } catch(...) {return 0;}
}

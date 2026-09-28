#include "imported_course.h"
#include "original_host_input.h"
#include <iostream>
using namespace idas3;using namespace idas3::original;
void require(bool ok,const char* text){if(!ok)throw std::runtime_error(text);}
int main(int argc,char** argv)try{
    if(argc!=3)throw std::runtime_error("native_root imported_root");
    const auto course=ImportedCourse::load(argv[2]);int surfaces=0,ticks=0;
    require(course.id==15?!course.lamps.empty():course.lamps.size()==(course.id==10?16:46),"Imported authored lamps were not loaded");
    if(course.id==15){
        // Independent surveyed points on the road-facing guardrail mesh, not
        // samples of the generated collision/path files. The old ribbon was
        // up to 2.31 m inside one rail and 1.51 m behind the opposite rail.
        struct Rail {int point;float x,z;};
        for(const auto rail:std::array<Rail,5>{{{3570,648.11699f,434.47448f},
                {3580,651.98541f,450.65958f},{3584,655.51729f,457.83108f},
                {3565,639.27911f,423.18845f},{3584,643.95111f,462.64543f}}}){
            auto p=course.center[rail.point];float dx=rail.x-p[0],dz=rail.z-p[2],length=std::hypot(dx,dz);dx/=length;dz/=length;
            for(float offset:{-.10f,.10f}){
                OriginalCollisionQuery q;clearOriginalCollisionQuery(q);
                q.setf(44,p[0]);q.setf(48,p[1]+1);q.setf(52,p[2]);
                q.setf(32,rail.x+dx*offset);q.setf(36,p[1]+1);q.setf(40,rail.z+dz*offset);
                OriginalTriangleSearchTrace trace;OriginalSurfaceScratch scratch;
                require(queryOriginalCollisionSwept(course.collision,q,trace,scratch),"Missing contact at surveyed Tsubaki guardrail");
                if(((q.u(28)&0x8000u)!=0)!=(offset>0))throw std::runtime_error("Collision does not align with visible Tsubaki guardrail at "+std::to_string(rail.point)+" offset "+std::to_string(offset));
                if(offset>0)require(q.f(24)>.05f&&q.f(24)<.13f,"Tsubaki guardrail collision depth differs from visible beam");
            }
        }
        // Surveyed end of the inner beam. Contact must stop here, without a
        // diagonal wall extending down the open pavement beyond the rail.
        for(float along:{-.5f,.5f}){
            const float x=646.67273f+along*.383f,z=470.18747f+along*.924f;
            OriginalCollisionQuery q;clearOriginalCollisionQuery(q);
            q.setf(44,x+.924f);q.setf(48,299);q.setf(52,z-.383f);
            q.setf(32,x-.0924f);q.setf(36,299);q.setf(40,z+.0383f);
            OriginalTriangleSearchTrace trace;OriginalSurfaceScratch scratch;
            require(queryOriginalCollisionSwept(course.collision,q,trace,scratch),"Missing road at rail endpoint");
            require(((q.u(28)&0x8000u)!=0)==(along<0),"Collision extends past visible rail endpoint or misses its tip");
        }
        // Put the actual AE86 footprint in the pavement which the old wall
        // crossed. Test both headings; endpoint-only lane traces cannot cover
        // the car's four wall probes or the source contact response.
        for(bool reverse:{false,true}){
            auto road=course.drivingRoad(reverse);OriginalDrivingSelection selection;
            selection.physics=makeOriginalFreshTimeAttackSelection(0,course.handlingCondition(reverse),OriginalWeather::Dry);
            selection.collisionVariant=unsigned(reverse);OriginalRacePoint position{649.928f,299.f,451.509f};
            OriginalCollisionQuery q;clearOriginalCollisionQuery(q);for(unsigned k=0;k<3;k++)q.setf(32+4*k,position[k]);
            OriginalTriangleSearchTrace trace;OriginalSurfaceScratch scratch;
            require(queryOriginalCollisionSurface(course.collision,q,trace,scratch),"Missing pavement beside surveyed rail");position[1]=q.f(16);
            auto tangent=course.source.points[3581]-course.source.points[3579];if(reverse)tangent=tangent*-1.f;
            OriginalDrivingSession session;session.reset(argv[1],selection,position,{0,std::atan2(-tangent.x,-tangent.z),0},&road);session.enableRaceStart(2);
            OriginalHostInputState input;
            for(int tick=0;tick<60;tick++){
                const auto effects=session.tick(adaptOriginalHostInput(input,{0,0,0,false,false},true,true,tick));
                require(!effects.invalidScalarDiagnostics&&effects.newImpactRecords.empty()&&session.vehicle().drive.u(0x150)==0,"AE86 hits invisible wall before visible Tsubaki rail");
            }
        }
        std::cout<<"PASS 12 surveyed guardrail clearance/contact/endpoint checks and 120 AE86 contact ticks\n";
    }
    // Query the converted road along its full length with the actual D3 solver.
    for(unsigned i=course.checkpoints[0];i<=unsigned(course.checkpoints[4]);i++){
        OriginalCollisionQuery q;clearOriginalCollisionQuery(q);auto p=course.center[i];
        q.setf(32,p[0]);q.setf(36,p[1]+1);q.setf(40,p[2]);OriginalTriangleSearchTrace trace;OriginalSurfaceScratch scratch;
        if(!queryOriginalCollisionSurface(course.collision,q,trace,scratch))throw std::runtime_error("Missing road surface at point "+std::to_string(i));
        require(std::abs(q.f(16)-p[1])<3,"Imported surface height mismatch");surfaces++;
    }
    // Sweep past the finite imported shoulders, including the first Sadamine
    // hairpin. Checking only the road centre never exercised these escapes.
    int wallSweeps=0;
    for(int i=course.checkpoints[0]+1;i<course.checkpoints[4]-1;++i)for(int side=0;side<2;++side)for(float outside:{6.f,12.f}){
        const auto& edge=side?course.left:course.right;
        OriginalRacePoint wall{},center{};
        for(unsigned k=0;k<3;++k){wall[k]=edge[i][k]*.63f+edge[i+1][k]*.37f;center[k]=course.center[i][k]*.63f+course.center[i+1][k]*.37f;}
        float dx=wall[0]-center[0],dz=wall[2]-center[2],length=std::sqrt(dx*dx+dz*dz);dx/=length;dz/=length;
        OriginalCollisionQuery q;clearOriginalCollisionQuery(q);
        q.setf(44,wall[0]-dx);q.setf(48,wall[1]+1);q.setf(52,wall[2]-dz);
        q.setf(32,wall[0]+dx*outside);q.setf(36,wall[1]+1);q.setf(40,wall[2]+dz*outside);
        OriginalTriangleSearchTrace trace;OriginalSurfaceScratch scratch;
        if(!queryOriginalCollisionSwept(course.collision,q,trace,scratch)||!(q.u(28)&0x8000))throw std::runtime_error("Missed imported wall at point "+std::to_string(i)+" side "+std::to_string(side)+" outside "+std::to_string(outside)+" triangle "+std::to_string(q.u(60))+" flags "+std::to_string(q.u(28)));
        require(std::isfinite(q.f(24))&&std::abs(q.f(0)*q.f(0)+q.f(8)*q.f(8)-1)<.001f,"Invalid imported wall contact");++wallSweeps;
    }
    std::cout<<"PASS "<<wallSweeps<<" whole-route outward wall sweeps\n";
    // Following a lane within the drivable ribbon must not hit a neighboring
    // shoulder, including the lower bridge beneath another part of Tsubaki.
    int laneSweeps=0,falseHits=0;
    for(int i=course.checkpoints[0]+1;i<course.checkpoints[4]-2;++i)for(float lane:{-.6f,0.f,.6f})for(bool reverse:{false,true}){
        const auto point=[&](int index){auto p=course.center[index];const auto& edge=lane<0?course.left[index]:course.right[index];
            for(unsigned k=0;k<3;++k)p[k]+=(edge[k]-p[k])*std::abs(lane);p[1]+=1;return p;};
        auto a=point(i),b=point(i+1);if(reverse)std::swap(a,b);
        OriginalCollisionQuery q;clearOriginalCollisionQuery(q);for(unsigned k=0;k<3;++k){q.setf(44+k*4,a[k]);q.setf(32+k*4,b[k]);}
        OriginalTriangleSearchTrace trace;OriginalSurfaceScratch scratch;
        if(!queryOriginalCollisionSwept(course.collision,q,trace,scratch)||(q.u(28)&0x8000u)){
            if(falseHits<20)std::cerr<<"False lane contact at "<<i<<" lane "<<lane<<" reverse "<<reverse<<" triangle "<<q.u(60)<<" flags "<<q.u(28)<<'\n';++falseHits;
        }++laneSweeps;
    }
    require(falseHits==0,"Interior lane sweeps hit a false wall");std::cout<<"PASS "<<laneSweeps<<" full-route interior lane sweeps\n";
    for(bool reverse:{false,true})for(float lane:{-.6f,0.f,.6f}){
        OriginalCollisionQuery q;clearOriginalCollisionQuery(q);
        int first=reverse?course.checkpoints[4]-1:course.checkpoints[0]+1;
        int last=reverse?course.checkpoints[0]+1:course.checkpoints[4]-1,step=reverse?-1:1;
        for(int i=first;i!=last;i+=step){
            for(unsigned k=0;k<3;++k){const auto& edge=lane<0?course.left:course.right;
                q.setf(44+k*4,course.center[i][k]+(edge[i][k]-course.center[i][k])*std::abs(lane)+(k==1?1:0));
                q.setf(32+k*4,course.center[i+step][k]+(edge[i+step][k]-course.center[i+step][k])*std::abs(lane)+(k==1?1:0));}
            OriginalTriangleSearchTrace trace;OriginalSurfaceScratch scratch;
            if(!queryOriginalCollisionSwept(course.collision,q,trace,scratch)||(q.u(28)&0x8000u))throw std::runtime_error("Cached lane contact at "+std::to_string(i)+" reverse "+std::to_string(reverse));
        }
    }
    std::cout<<"PASS cached lane traversal in both directions\n";
    for(bool reverse:{false,true}){
        const auto spawn=course.spawn(reverse);auto road=course.drivingRoad(reverse);auto projection=course.racePath(reverse);
        OriginalPathCoordinate coordinate{course.rules(reverse).startIndex,0};require(projection.project(spawn.position,coordinate,true),"Start is outside race path");
        OriginalRaceRules rules;course.resetRules(rules,reverse,spawn.position);rules.start();
        // Test all source gates, including fourth split, through existing rules.
        int extensions=0,sections=0;
        for(int index=rules.rules().startIndex;index<=rules.rules().startIndex+rules.rules().goalIndex;index++){
            int source=reverse?int(course.center.size())-1-index:index;
            auto e=rules.tick({index,0},course.center[source]);extensions+=e.timeExtension;sections+=e.section;
        }
        require(rules.state().phase==OriginalRacePhase::Finished&&extensions==3&&sections==4,"Original rules did not finish imported race/checkpoints");
        for(bool wet:{false,true})for(bool automatic:{false,true}){
            OriginalDrivingSelection selection;selection.physics=makeOriginalFreshTimeAttackSelection(0,course.handlingCondition(reverse),wet?OriginalWeather::Wet:OriginalWeather::Dry);selection.collisionVariant=unsigned(reverse);
            OriginalDrivingSession session;session.reset(argv[1],selection,spawn.position,spawn.angles,&road);session.enableRaceStart(2);OriginalHostInputState input;
            require(session.vehicle().drive.u(0x434)==unsigned(wet),"D3 wet handling flag not applied");
            float maximumSpeed=0,maximumRpm=0;unsigned maximumGear=0,contacts=0;
            for(int t=0;t<600;t++){
                auto effects=session.tick(adaptOriginalHostInput(input,{0,1,0,false,t==180||t==330},automatic,true,t));ticks++;
                const auto& v=session.vehicle();maximumSpeed=std::max(maximumSpeed,v.drive.f(0x238));maximumRpm=std::max(maximumRpm,v.transmission.tach1c);maximumGear=std::max(maximumGear,v.transmission.gear00);
                require(!effects.invalidScalarDiagnostics&&std::isfinite(v.drive.f(4)),"Original vehicle failed on imported road");
                contacts+=effects.newImpactRecords.size();
            }
            std::cout<<"direction "<<reverse<<" wet "<<wet<<" automatic "<<automatic<<" speed "<<maximumSpeed<<" rpm "<<maximumRpm<<" gear "<<maximumGear<<'\n';
            require(maximumSpeed>4&&maximumRpm>2000&&maximumGear>1,"Imported race did not drive/shift with D3 vehicle");
            // Hold into the edge intentionally: the existing wall solver must
            // stop lateral escape rather than letting the car fall off the strip.
            for(int t=0;t<180;t++){
                auto e=session.tick(adaptOriginalHostInput(input,{.8f,1,0,false,false},automatic,true,t));ticks++;contacts+=e.newImpactRecords.size();
                const auto& v=session.vehicle().drive;auto p=course.source.project({v.f(0),v.f(4),v.f(8)});
                if(std::abs(v.f(4)-p.sample.center.y)>=5)throw std::runtime_error("D3 car height mismatch at boundary: tick="+std::to_string(t)+" position="+std::to_string(v.f(0))+","+std::to_string(v.f(4))+","+std::to_string(v.f(8))+" roadHeight="+std::to_string(p.sample.center.y)+" segment="+std::to_string(p.sample.segmentIndex));
            }
            require(contacts>0,"Imported boundary did not invoke D3 wall contact");
        }
    }
    std::cout<<"PASS "<<surfaces<<" road queries; both directions/four sections/three extensions; "<<ticks<<" D3 solver ticks with automatic and manual gearbox.\n";
}catch(const std::exception& e){std::cerr<<e.what()<<'\n';return 1;}

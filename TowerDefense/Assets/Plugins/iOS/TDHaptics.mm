// TDHaptics.mm - Taptic Engine feedback for the tower defense game.
//
// Called from C# (TowerDefense.Platform.Haptics) via [DllImport("__Internal")].
// Unity copies files under Assets/Plugins/iOS into the generated Xcode project
// (Libraries/Plugins/iOS) and compiles them into UnityFramework. Only UIKit is
// used, which every Unity iOS project already links - no extra frameworks,
// no networking, no private API.
//
// UIFeedbackGenerator must be used on the main thread. Unity's iOS player loop
// (and therefore every C# call into this file) already runs on the main
// thread; the dispatch below is only a safety net.
//
// On devices without a Taptic Engine (iPads, iPhone 6s and older) the system
// silently ignores these calls.

#import <UIKit/UIKit.h>

static UIImpactFeedbackGenerator* s_impact[3];
static UINotificationFeedbackGenerator* s_notification;
static UISelectionFeedbackGenerator* s_selection;

static void TDRunOnMain(dispatch_block_t block)
{
    if ([NSThread isMainThread]) block();
    else dispatch_async(dispatch_get_main_queue(), block);
}

static UIImpactFeedbackGenerator* TDImpactGenerator(int style)
{
    if (style < 0 || style > 2) style = 1;
    if (s_impact[style] == nil)
    {
        UIImpactFeedbackStyle s = style == 0 ? UIImpactFeedbackStyleLight
                                : style == 2 ? UIImpactFeedbackStyleHeavy
                                             : UIImpactFeedbackStyleMedium;
        s_impact[style] = [[UIImpactFeedbackGenerator alloc] initWithStyle:s];
    }
    return s_impact[style];
}

static UINotificationFeedbackGenerator* TDNotificationGenerator(void)
{
    if (s_notification == nil) s_notification = [[UINotificationFeedbackGenerator alloc] init];
    return s_notification;
}

static UISelectionFeedbackGenerator* TDSelectionGenerator(void)
{
    if (s_selection == nil) s_selection = [[UISelectionFeedbackGenerator alloc] init];
    return s_selection;
}

extern "C" {

// style: 0 = light, 1 = medium, 2 = heavy
void TDHaptics_Impact(int style)
{
    TDRunOnMain(^{
        UIImpactFeedbackGenerator* g = TDImpactGenerator(style);
        [g impactOccurred];
        [g prepare]; // keep the engine warm for the next hit
    });
}

// type: 0 = success, 1 = warning, 2 = error
void TDHaptics_Notification(int type)
{
    TDRunOnMain(^{
        UINotificationFeedbackType t = type == 1 ? UINotificationFeedbackTypeWarning
                                     : type == 2 ? UINotificationFeedbackTypeError
                                                 : UINotificationFeedbackTypeSuccess;
        UINotificationFeedbackGenerator* g = TDNotificationGenerator();
        [g notificationOccurred:t];
        [g prepare];
    });
}

void TDHaptics_Selection(void)
{
    TDRunOnMain(^{
        UISelectionFeedbackGenerator* g = TDSelectionGenerator();
        [g selectionChanged];
        [g prepare];
    });
}

// Wake the Taptic Engine ahead of an expected burst (e.g. when a level starts).
void TDHaptics_Prepare(void)
{
    TDRunOnMain(^{
        for (int i = 0; i < 3; i++) [TDImpactGenerator(i) prepare];
        [TDNotificationGenerator() prepare];
        [TDSelectionGenerator() prepare];
    });
}

// Drop the generators (low-memory warning or app backgrounded).
void TDHaptics_Release(void)
{
    TDRunOnMain(^{
        for (int i = 0; i < 3; i++) s_impact[i] = nil;
        s_notification = nil;
        s_selection = nil;
    });
}

} // extern "C"
